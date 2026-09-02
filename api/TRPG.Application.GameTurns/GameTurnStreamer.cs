using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Narration;
using TRPG.Application.Narration.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal abstract record GameTurnPrompt
{
    public sealed record Reply(string Text) : GameTurnPrompt;

    public sealed record Narrate(string Text, bool IncludeTools = true) : GameTurnPrompt;

    public sealed record None : GameTurnPrompt;
}

internal class GameTurnStreamer(
    LlmConversationClient llmConversationClient,
    ICommandHandler<CloseLingeringNpcConversationsCommand> closeLingeringConversations,
    GameTurnContext turnContext,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<AdvanceTimeCommand, TimeSpan> advanceTime,
    IQueryHandler<
        GetLoreAnchorAutomatonByWorldQuery,
        LoreAnchorAutomaton
    > getLoreAnchorAutomatonByWorld,
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents,
    IGameClientEventDispatcher eventDispatcher,
    IGameClientEventAckGate eventAckGate,
    IOptionsSnapshot<GameClockOptions> gameClockOptions,
    ILogger<GameTurnStreamer> logger
)
{
    public async IAsyncEnumerable<string> StreamTurn(
        GameTurnSession session,
        Func<CancellationToken, Task<GameTurnPrompt>> resolveTurn,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Captured before resolveTurn runs so the diff below also catches direct-command mutations, not just tool calls.
        var before = await GetScene(session, cancellationToken);

        var prompt = await resolveTurn(cancellationToken);

        if (prompt is GameTurnPrompt.Reply reply)
        {
            yield return reply.Text;
            yield break;
        }

        if (prompt is GameTurnPrompt.None)
        {
            // No narration follows, so there's no ordering race to guard with an ack-wait.
            await EnqueueSceneChange(before, session, cancellationToken);
            await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);
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

            await foreach (
                var token in StreamNarration(
                    before,
                    session,
                    streamedReply.Tokens,
                    cancellationToken
                )
            )
            {
                yield return token;
            }

            await FinishTurn(streamedReply.InputOrdinal, cancellationToken);
        }
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
        var flushed = false;

        await foreach (var token in linkedTokens)
        {
            if (!flushed)
            {
                await FlushSceneChange(before, session, cancellationToken);
                flushed = true;
            }

            yield return token;
        }

        if (!flushed)
        {
            await FlushSceneChange(before, session, cancellationToken);
        }
    }

    private async Task<SceneResult> GetScene(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        return await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Playtime = playtime,
            },
            cancellationToken
        );
    }

    private async Task<SceneResult> EnqueueSceneChange(
        SceneResult before,
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var after = await GetScene(session, cancellationToken);

        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
        {
            gameEvents.Enqueue(new SceneUpdatedEvent(after));
        }

        return after;
    }

    private async Task FlushSceneChange(
        SceneResult before,
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        await EnqueueSceneChange(before, session, cancellationToken);
        await eventAckGate.FlushAndAwaitAckAsync(session.WorldId, cancellationToken);
    }

    private async Task BeginTurn(GameTurnSession session, CancellationToken cancellationToken)
    {
        turnContext.SessionId = session.SessionId;
        turnContext.WorldId = session.WorldId;
        turnContext.PlayerId = session.PlayerId;
        turnContext.PlayerMoved = false;

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                Playtime = playtime,
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
