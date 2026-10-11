using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Narration;
using TRPG.Application.Narration.Queries;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class GameTurnStreamer(
    LlmConversationClient llmConversationClient,
    ICommandHandler<CloseLingeringNpcConversationsCommand> closeLingeringConversations,
    GameTurnContext turnContext,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    IQueryHandler<
        GetLoreAnchorAutomatonByWorldQuery,
        LoreAnchorAutomaton
    > getLoreAnchorAutomatonByWorld,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IQueryHandler<GetOpenNpcConversationsQuery, Dictionary<string, Guid>> getOpenNpcConversations,
    TurnSceneDiffer sceneDiffer,
    IGameClientEventDispatcher eventDispatcher,
    WorldSimulationCoordinator simulation,
    ILogger<GameTurnStreamer> logger
)
{
    public async IAsyncEnumerable<string> StreamChat(
        GameTurnSession session,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var before = await CaptureScene(session, cancellationToken);

        await BeginTurn(session, cancellationToken);

        var streamedReply = await llmConversationClient.StreamReply(message, cancellationToken);

        await foreach (
            var token in StreamNarration(before, session, streamedReply.Tokens, cancellationToken)
        )
        {
            yield return token;
        }

        await FinishTurn(streamedReply.InputOrdinal, cancellationToken);
    }

    private async IAsyncEnumerable<string> StreamNarration(
        SceneResult before,
        GameTurnSession session,
        IAsyncEnumerable<string> tokens,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var automaton = await getLoreAnchorAutomatonByWorld.Handle(
            new GetLoreAnchorAutomatonByWorldQuery { WorldId = session.WorldId },
            cancellationToken
        );

        var linkedTokens = LoreAnchorLinker.Link(tokens, automaton, cancellationToken);

        // The state change already happened, so the client must learn of it before the narration describing it.
        var flushedInvocations = -1;
        var narration = new StringBuilder();

        await foreach (var token in linkedTokens)
        {
            // A tool can enqueue events after the model has already emitted introductory text.
            if (flushedInvocations != turnContext.ToolInvocations)
            {
                flushedInvocations = turnContext.ToolInvocations;
                await FlushSceneChange(session, cancellationToken);
            }

            narration.Append(token);
            yield return token;
        }

        if (flushedInvocations != turnContext.ToolInvocations)
        {
            await FlushSceneChange(session, cancellationToken);
        }

        await LogUnbriefedNpcMentions(before, narration.ToString(), session, cancellationToken);
    }

    private async Task LogUnbriefedNpcMentions(
        SceneResult before,
        string narration,
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        if (before.NearbyCreatures.Count == 0)
        {
            return;
        }

        var openConversations = await getOpenNpcConversations.Handle(
            new GetOpenNpcConversationsQuery { SessionId = session.SessionId },
            cancellationToken
        );

        foreach (var creature in before.NearbyCreatures)
        {
            if (openConversations.ContainsKey(creature.Name))
            {
                continue;
            }

            if (Regex.IsMatch(narration, $@"\b{Regex.Escape(creature.Name)}\b"))
            {
                logger.LogWarning(
                    "[game] Narration mentioned {NpcName} without an open start_conversation this turn",
                    creature.Name
                );
            }
        }
    }

    private async Task<SceneResult> CaptureScene(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        return await simulation.Request(
            session.WorldId,
            async (_, token) =>
            {
                return await sceneDiffer.Capture(session, token);
            },
            cancellationToken
        );
    }

    private async Task FlushSceneChange(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        await simulation.Request(
            session.WorldId,
            async (_, token) =>
            {
                await sceneDiffer.EnqueueChange(session, token);
            },
            cancellationToken
        );
        await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);
    }

    private Task BeginTurn(GameTurnSession session, CancellationToken cancellationToken)
    {
        turnContext.SessionId = session.SessionId;
        turnContext.WorldId = session.WorldId;
        turnContext.PlayerId = session.PlayerId;

        return simulation.Request(
            session.WorldId,
            (_, token) => ApplyTurnRegen(session, token),
            cancellationToken
        );
    }

    private async Task ApplyTurnRegen(GameTurnSession session, CancellationToken cancellationToken)
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                GameTime = gameTime,
                CreatureIds = [turnContext.PlayerId],
            },
            cancellationToken
        );
    }

    private async Task FinishTurn(int currentTurnStart, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (turnContext.PlayerMoved)
        {
            var gameTime = await getGameTime.Handle(
                new GetGameTimeQuery { SessionId = turnContext.SessionId },
                cancellationToken
            );
            await closeLingeringConversations.Handle(
                new CloseLingeringNpcConversationsCommand
                {
                    SessionId = turnContext.SessionId,
                    WorldId = turnContext.WorldId,
                    PlayerId = turnContext.PlayerId,
                    GameTime = gameTime,
                    CurrentTurnStart = currentTurnStart,
                },
                cancellationToken
            );
        }

        logger.LogInformation(
            "[perf] FinishTurn took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );
    }
}
