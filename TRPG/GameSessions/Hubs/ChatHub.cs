using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Contracts.Combat.Requests;
using TRPG.Data.Models;
using TRPG.Players.Endpoints;

namespace TRPG.GameSessions.Hubs;

internal sealed class ChatHub(
    GameTurnRunner turnRunner,
    GameTurnContext turnContext,
    EndGameSessionCommandHandler endGameSession,
    GetGameSessionQueryHandler getGameSession,
    GetEntityNameAutomatonByWorldQueryHandler getEntityNameAutomatonByWorld,
    WorldConnectionRegistry worldConnections,
    GetCreatureByIdQueryHandler getCreatureById,
    GetActiveFightCombatantsQueryHandler getActiveFightCombatants
) : Hub
{
    private const string GameSessionKey = "GameSession";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();

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
                PlayerEndpoints.ToFightState(combatants)
            );
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[GameSessionKey] is GameSession gameSession)
        {
            worldConnections.Remove(gameSession.WorldId, Context.ConnectionId);
            await endGameSession.Handle(new EndGameSessionCommand { SessionId = gameSession.Id });
        }

        await base.OnDisconnectedAsync(exception);
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
            yield return token;
        }

        await PushPendingEvents();
    }

    private async Task PushPendingEvents()
    {
        foreach (var turnEvent in turnContext.PendingEvents)
        {
            switch (turnEvent)
            {
                case CombatStartedEvent combatStarted:
                    await Clients.Caller.SendAsync(
                        "CombatStarted",
                        PlayerEndpoints.ToFightState(combatStarted.Combatants)
                    );
                    break;
                case CombatEndedEvent:
                    await Clients.Caller.SendAsync("CombatEnded");
                    break;
            }
        }

        turnContext.PendingEvents.Clear();
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
