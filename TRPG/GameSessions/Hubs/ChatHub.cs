using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Contracts.Combat.Requests;
using TRPG.Data.Models;

namespace TRPG.GameSessions.Hubs;

internal sealed class ChatHub(
    GameTurnRunner turnRunner,
    GameTurnContext turnContext,
    EndGameSessionCommandHandler endGameSession,
    GetGameSessionQueryHandler getGameSession,
    WorldConnectionRegistry worldConnections,
    GetCreatureByIdQueryHandler getCreatureById
) : Hub
{
    private const string SessionIdKey = "SessionId";
    private const string WorldIdKey = "WorldId";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        Context.Items[SessionIdKey] = sessionId;

        var worldId = await ResolveWorldId(sessionId);
        Context.Items[WorldIdKey] = worldId;

        if (!worldConnections.TryAdd(worldId, Context.ConnectionId))
        {
            throw new HubException("Another connection is already active for this world.");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[WorldIdKey] is Guid worldId)
        {
            worldConnections.Remove(worldId, Context.ConnectionId);
        }

        if (Context.Items[SessionIdKey] is Guid sessionId)
        {
            await endGameSession.Handle(new EndGameSessionCommand { SessionId = sessionId });
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<Guid> ResolveWorldId(Guid sessionId)
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId }
        );
        return snapshot.WorldId;
    }

    private async Task<bool> IsPlayerDead(CancellationToken cancellationToken)
    {
        var gameSession = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = gameSession.PlayerId },
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
        turnContext.SessionId = (Guid)Context.Items[SessionIdKey]!;
        return StreamTurn(tokens, cancellationToken);
    }

    private async IAsyncEnumerable<string> StreamTurn(
        IAsyncEnumerable<string> tokens,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        if (await IsPlayerDead(cancellationToken))
        {
            yield return "You have died. This adventure has come to an end.";
            yield break;
        }

        await foreach (var token in tokens.WithCancellation(cancellationToken))
        {
            yield return token;
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
