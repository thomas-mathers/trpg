using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Combat.Requests;
using TRPG.Contracts.Combat.Responses;
using TRPG.Data.Models;

namespace TRPG.GameSessions.Hubs;

internal sealed class ChatHub(
    GameTurnRunner turnRunner,
    GameTurnContext turnContext,
    IGameClientEventPublisher gameEvents,
    GameClientEventDispatcher eventDispatcher,
    GetGameSessionQueryHandler getGameSession,
    EndGameSessionCommandHandler endGameSession,
    GetEntityNameAutomatonByWorldQueryHandler getEntityNameAutomatonByWorld,
    PendingSessionEndRegistry pendingSessionEnds,
    GetCreatureByIdQueryHandler getCreatureById,
    GetActiveFightCombatantsQueryHandler getActiveFightCombatants,
    GetSceneQueryHandler getScene,
    GetPlaytimeQueryHandler getPlaytime
) : Hub
{
    private const string GameSessionKey = "GameSession";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        pendingSessionEnds.Cancel(sessionId);

        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId }
        );
        Context.Items[GameSessionKey] = snapshot;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GameClientGroups.ForWorld(snapshot.WorldId)
        );

        await base.OnConnectedAsync();

        await PushSceneSnapshot(snapshot);
        await PushActiveCombatState(snapshot.PlayerId);
    }

    private async Task PushSceneSnapshot(GameSession gameSession)
    {
        var scene = await GetCurrentScene(gameSession, Context.ConnectionAborted);
        await Clients.Caller.SendAsync(
            "SceneSnapshot",
            SceneSnapshotMapper.ToSnapshot(scene),
            Context.ConnectionAborted
        );
    }

    private async Task PushActiveCombatState(Guid playerId)
    {
        var combatants = await getActiveFightCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = playerId }
        );

        if (combatants.Count > 0)
        {
            await Clients.Caller.SendAsync(
                "CombatStarted",
                FightStateMapper.ToFightState(combatants)
            );
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[GameSessionKey] is GameSession gameSession)
        {
            pendingSessionEnds.Schedule(gameSession.Id);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task EndSession()
    {
        var gameSession = (GameSession)Context.Items[GameSessionKey]!;
        pendingSessionEnds.Cancel(gameSession.Id);

        try
        {
            await endGameSession.Handle(new EndGameSessionCommand { SessionId = gameSession.Id });
        }
        catch (EntityNotFoundException)
        {
            // Already ended some other way; nothing left to clean up.
        }
    }

    private async Task<bool> IsPlayerDead(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = playerId },
            cancellationToken
        );
        return player?.State == CreatureState.Dead;
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken) =>
        RunTurn(turnRunner.StreamOpeningResponse(cancellationToken), cancellationToken);

    public IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken) =>
        RunTurn(turnRunner.StreamResponse(message, cancellationToken), cancellationToken);

    public IAsyncEnumerable<string> SendWait(int hours, CancellationToken cancellationToken) =>
        RunTurn(turnRunner.StreamWaitResponse(hours, cancellationToken), cancellationToken);

    public IAsyncEnumerable<string> SendFlee(CancellationToken cancellationToken) =>
        RunTurn(turnRunner.StreamFleeResponse(cancellationToken), cancellationToken);

    private IAsyncEnumerable<string> RunTurn(
        IAsyncEnumerable<string> tokens,
        CancellationToken cancellationToken
    )
    {
        var gameSession = (GameSession)Context.Items[GameSessionKey]!;
        turnContext.SessionId = gameSession.Id;
        return StreamTurn(gameSession, tokens, cancellationToken);
    }

    private async IAsyncEnumerable<string> StreamTurn(
        GameSession gameSession,
        IAsyncEnumerable<string> tokens,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        if (await IsPlayerDead(gameSession.PlayerId, cancellationToken))
        {
            yield return "You have died. This adventure has come to an end.";
            yield break;
        }

        var before = await GetCurrentScene(gameSession, cancellationToken);

        var automaton = await getEntityNameAutomatonByWorld.Handle(
            new GetEntityNameAutomatonByWorldQuery { WorldId = gameSession.WorldId },
            cancellationToken
        );

        var linkedTokens = NarrationEntityLinker.Link(tokens, automaton, cancellationToken);

        await foreach (var token in linkedTokens)
        {
            await eventDispatcher.FlushAsync(gameSession.WorldId, cancellationToken);

            yield return token;
        }

        var after = await GetCurrentScene(gameSession, cancellationToken);

        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
        {
            gameEvents.Publish(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(after),
                    SceneUpdateReason.Synced
                )
            );
        }

        await eventDispatcher.FlushAsync(gameSession.WorldId, cancellationToken);
    }

    private async Task<SceneResult> GetCurrentScene(
        GameSession gameSession,
        CancellationToken cancellationToken
    )
    {
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = gameSession.Id },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        return await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = gameSession.WorldId,
                PlayerId = gameSession.PlayerId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );
    }

    private Guid GetSessionIdFromQuery()
    {
        var raw = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var sessionId))
        {
            throw new HubException("A valid sessionId query parameter is required.");
        }

        return sessionId;
    }
}

internal sealed class PendingSessionEndRegistry(
    IServiceScopeFactory serviceScopeFactory,
    IOptionsMonitor<GameSessionOptions> options,
    ILogger<PendingSessionEndRegistry> logger
)
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pendingEnds = new();

    public void Schedule(Guid sessionId)
    {
        Cancel(sessionId);

        var cts = new CancellationTokenSource();
        _pendingEnds[sessionId] = cts;
        _ = RunAfterDelay(sessionId, options.CurrentValue.SessionEndGracePeriod, cts);
    }

    public void Cancel(Guid sessionId)
    {
        if (_pendingEnds.TryRemove(sessionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task RunAfterDelay(Guid sessionId, TimeSpan delay, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delay, cts.Token);

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var endGameSession =
                scope.ServiceProvider.GetRequiredService<EndGameSessionCommandHandler>();
            await endGameSession.Handle(
                new EndGameSessionCommand { SessionId = sessionId },
                cts.Token
            );
        }
        catch (OperationCanceledException)
        {
            // The player reconnected within the grace period; nothing to end.
        }
        catch (EntityNotFoundException)
        {
            // Already ended some other way; nothing left to clean up.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to end session {SessionId} after disconnect", sessionId);
        }
        finally
        {
            if (_pendingEnds.TryGetValue(sessionId, out var current) && current == cts)
            {
                _pendingEnds.TryRemove(sessionId, out _);
            }

            cts.Dispose();
        }
    }
}
