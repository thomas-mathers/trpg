using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain.Models;
using TRPG.GameSessions.Commands;
using TypedSignalR.Client;

namespace TRPG.GameSessions.Hubs;

[Hub]
public interface IChatHub
{
    Task EndSession();
    IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendWait(int hours, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendFlee(CancellationToken cancellationToken);
    Task ResolveUseAbilityCombatAction(Guid targetId, string abilityName);
    Task ResolveUseItemCombatAction(string itemName);
    IAsyncEnumerable<string> ResolveAttackEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveEvadeEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveRetreatEncounterAction(CancellationToken cancellationToken);
}

internal sealed class ChatHub(
    GameTurnRunner gameTurnRunner,
    GameClientEventDispatcher eventDispatcher,
    ICommandHandler<PublishSessionStateCommand> publishSessionState,
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    ICommandHandler<EndGameSessionCommand> endGameSession,
    PendingSessionEndRegistry pendingSessionEnds
) : Hub<IGameClient>, IChatHub
{
    private const string SessionKey = "Session";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        pendingSessionEnds.Cancel(sessionId);

        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId }
        );
        var session = new GameTurnSession(snapshot.Id, snapshot.WorldId, snapshot.PlayerId);
        Context.Items[SessionKey] = session;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GameClientGroups.ForWorld(session.WorldId)
        );

        await base.OnConnectedAsync();

        await publishSessionState.Handle(
            new PublishSessionStateCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
            },
            Context.ConnectionAborted
        );
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[SessionKey] is GameTurnSession session)
        {
            pendingSessionEnds.Schedule(session.SessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task EndSession()
    {
        pendingSessionEnds.Cancel(Session.SessionId);

        try
        {
            await endGameSession.Handle(
                new EndGameSessionCommand { SessionId = Session.SessionId }
            );
        }
        catch (EntityNotFoundException)
        {
            // Already ended some other way; nothing left to clean up.
        }
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamOpening(Session, cancellationToken);

    public IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken) =>
        gameTurnRunner.StreamChat(Session, message, cancellationToken);

    public IAsyncEnumerable<string> SendWait(int hours, CancellationToken cancellationToken) =>
        gameTurnRunner.StreamWait(Session, hours, cancellationToken);

    public IAsyncEnumerable<string> SendFlee(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamFlee(Session, cancellationToken);

    public IAsyncEnumerable<string> ResolveAttackEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamEncounterAction(
            Session,
            new AttackEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveEvadeEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamEncounterAction(
            Session,
            new EvadeEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveRetreatEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamEncounterAction(
            Session,
            new RetreatEncounterAction(),
            cancellationToken
        );

    public Task ResolveUseAbilityCombatAction(Guid targetId, string abilityName) =>
        ResolveCombatAction(new UseAbilityAction(targetId, abilityName));

    public Task ResolveUseItemCombatAction(string itemName) =>
        ResolveCombatAction(new UseItemAction(itemName));

    private async Task ResolveCombatAction(PlayerCombatAction action)
    {
        await gameTurnRunner.ResolveCombatAction(Session, action, Context.ConnectionAborted);
        await eventDispatcher.FlushAsync(Session.WorldId, Context.ConnectionAborted);
    }

    private GameTurnSession Session => (GameTurnSession)Context.Items[SessionKey]!;

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
            var endGameSession = scope.ServiceProvider.GetRequiredService<
                ICommandHandler<EndGameSessionCommand>
            >();
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
