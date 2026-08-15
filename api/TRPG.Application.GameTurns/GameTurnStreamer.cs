using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Narration;
using TRPG.Application.Narration.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Mappers;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.GameTurns;

internal abstract record GameTurnPrompt
{
    public sealed record Reply(string Text) : GameTurnPrompt;

    public sealed record Narrate(string Text, bool IncludeTools = true) : GameTurnPrompt;
}

internal class GameTurnStreamer(
    LlmConversationClient llmConversationClient,
    CloseLingeringNpcConversationsCommandHandler closeLingeringConversations,
    GameTurnContext turnContext,
    ApplyPassiveRegenCommandHandler applyPassiveRegen,
    AdvanceTimeCommandHandler advanceTime,
    GetLoreAnchorAutomatonByWorldQueryHandler getLoreAnchorAutomatonByWorld,
    GetCurrentSceneQueryHandler getCurrentScene,
    IGameClientEventSink gameEvents,
    IGameClientEventDispatcher eventDispatcher,
    IOptionsSnapshot<GameClockOptions> gameClockOptions,
    ILogger<GameTurnStreamer> logger
)
{
    public async IAsyncEnumerable<string> StreamTurn(
        GameSessionIdentity session,
        Func<CancellationToken, Task<GameTurnPrompt>> resolveTurn,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var prompt = await resolveTurn(cancellationToken);

        if (prompt is GameTurnPrompt.Reply reply)
        {
            yield return reply.Text;
            yield break;
        }

        if (prompt is GameTurnPrompt.Narrate narrate)
        {
            await BeginTurn(session, cancellationToken);

            await advanceTime.Handle(
                new AdvanceTimeCommand
                {
                    SessionId = session.SessionId,
                    Delta = gameClockOptions.Value.RealTimePerMessage,
                },
                cancellationToken
            );

            var streamedReply = await llmConversationClient.StreamReply(
                narrate.Text,
                narrate.IncludeTools,
                cancellationToken
            );

            await foreach (var token in StreamNarration(streamedReply.Tokens, cancellationToken))
            {
                yield return token;
            }

            await FinishTurn(streamedReply.InputOrdinal, cancellationToken);
        }
    }

    private async IAsyncEnumerable<string> StreamNarration(
        IAsyncEnumerable<string> tokens,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var before = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
            },
            cancellationToken
        );

        var automaton = await getLoreAnchorAutomatonByWorld.Handle(
            new GetLoreAnchorAutomatonByWorldQuery { WorldId = turnContext.WorldId },
            cancellationToken
        );

        var linkedTokens = LoreAnchorLinker.Link(tokens, automaton, cancellationToken);

        await foreach (var token in linkedTokens)
        {
            yield return token;
        }

        var after = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                SessionId = turnContext.SessionId,
            },
            cancellationToken
        );

        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
        {
            gameEvents.Enqueue(new SceneUpdatedEvent(SceneSnapshotMapper.ToSnapshot(after)));
        }

        await eventDispatcher.FlushAsync(turnContext.WorldId, cancellationToken);
    }

    private async Task BeginTurn(GameSessionIdentity session, CancellationToken cancellationToken)
    {
        turnContext.SessionId = session.SessionId;
        turnContext.WorldId = session.WorldId;
        turnContext.PlayerId = session.PlayerId;
        turnContext.PlayerMoved = false;

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = turnContext.SessionId,
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
            await closeLingeringConversations.Handle(
                new CloseLingeringNpcConversationsCommand
                {
                    SessionId = turnContext.SessionId,
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
