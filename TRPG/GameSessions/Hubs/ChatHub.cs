using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
    GetActiveFightCombatantsQueryHandler getActiveFightCombatants
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

    // Called by the client before deliberately leaving (e.g. "Exit to Main Menu"), so the
    // session ends immediately rather than racing the disconnect grace period — otherwise
    // starting a new session too soon after exiting would read a stale World.Playtime, since
    // that only gets flushed once the old session actually ends.
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

        await PushPendingEvents();
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
