using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Exceptions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Combat.Requests;
using TRPG.Data.Models;

namespace TRPG.GameSessions.Hubs;

internal sealed class ChatHub(
    GameTurnRunner turnRunner,
    GameTurnContext turnContext,
    GetGameSessionQueryHandler getGameSession,
    EndGameSessionCommandHandler endGameSession,
    GetEntityNameAutomatonByWorldQueryHandler getEntityNameAutomatonByWorld,
    WorldConnectionRegistry worldConnections,
    PendingSessionEndRegistry pendingSessionEnds,
    GetCreatureByIdQueryHandler getCreatureById,
    GetActiveFightCombatantsQueryHandler getActiveFightCombatants,
    GetSceneQueryHandler getScene
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

        if (!worldConnections.TryAdd(snapshot.WorldId, Context.ConnectionId))
        {
            throw new HubException("Another connection is already active for this world.");
        }

        await base.OnConnectedAsync();

        await PushActiveCombatState(snapshot.PlayerId);
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
            worldConnections.Remove(gameSession.WorldId, Context.ConnectionId);
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
        catch (GameSessionNotFoundException)
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

    public IAsyncEnumerable<string> SendCombatAction(
        PlayerCombatAction action,
        CancellationToken cancellationToken
    ) =>
        RunTurn(
            turnRunner.StreamCombatActionResponse(action, cancellationToken),
            cancellationToken
        );

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

        // Captured before any turn logic runs, so it can be diffed against the same snapshot
        // recomputed after the turn completes - this is what notices a nearby creature dying (or
        // any other scene-affecting change) regardless of which command handler caused it, instead
        // of requiring every mutation site to remember to enqueue a SceneUpdatedEvent itself.
        var before = await GetCurrentScene(gameSession, cancellationToken);

        var automaton = await getEntityNameAutomatonByWorld.Handle(
            new GetEntityNameAutomatonByWorldQuery { WorldId = gameSession.WorldId },
            cancellationToken
        );

        var linkedTokens = NarrationEntityLinker.Link(tokens, automaton, cancellationToken);

        await foreach (var token in linkedTokens)
        {
            if (turnContext.PendingEvents.Count > 0)
            {
                await PushPendingEvents();
            }

            yield return token;
        }

        var after = await GetCurrentScene(gameSession, cancellationToken);

        // Record-generated equality doesn't deep-compare collection-typed properties (SceneResult
        // is full of them), so a JSON string comparison is used instead - it reflects collection
        // contents, unlike Equals()/==, which would fall back to reference equality on those
        // properties and report "changed" on every turn regardless of any real difference.
        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
        {
            turnContext.PendingEvents.Enqueue(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(after),
                    SceneUpdateReason.Synced
                )
            );
        }

        await PushPendingEvents();
    }

    private async Task<SceneResult> GetCurrentScene(
        GameSession gameSession,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = gameSession.Id },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(session.Playtime);

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

    private async Task PushPendingEvents()
    {
        var sentMethodNames = new HashSet<string>();
        while (turnContext.PendingEvents.TryDequeue(out var turnEvent))
        {
            if (!sentMethodNames.Add(turnEvent.MethodName))
            {
                continue;
            }

            if (turnEvent.Payload != null)
            {
                await Clients.Caller.SendAsync(turnEvent.MethodName, turnEvent.Payload);
            }
            else
            {
                await Clients.Caller.SendAsync(turnEvent.MethodName);
            }
        }
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

internal sealed class WorldConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connectionIdsByWorldId = new();

    public bool TryAdd(Guid worldId, string connectionId) =>
        _connectionIdsByWorldId.TryAdd(worldId, connectionId);

    public void Remove(Guid worldId, string connectionId) =>
        _connectionIdsByWorldId.TryRemove(new KeyValuePair<Guid, string>(worldId, connectionId));
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
        catch (GameSessionNotFoundException)
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
