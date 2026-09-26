using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
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
    IAsyncEnumerable<string> SendWait(int hours, int minutes, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendSitDown(Guid seatId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendStandUp(CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendSleep(int hours, int minutes, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendActivateTrigger(
        Guid triggerId,
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> SendAcceptQuest(Guid questId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendDeclineQuest(Guid questId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendCompleteQuest(Guid questId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendDeliverItem(Guid recipientId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendPurchaseCaravanTicket(
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> SendDeclineCaravanTicket(CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendBoardCaravan(Guid caravanId, CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendFlee(CancellationToken cancellationToken);
    IAsyncEnumerable<string> SendRespawn(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveUseAbilityCombatAction(
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> ResolveUseItemCombatAction(
        string itemName,
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> ResolveAttackEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveFleeEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveIntimidateEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolvePayTollEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveFightEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveFleeShakedownEncounterAction(
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> ResolvePayFineEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveGoToJailEncounterAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveResistArrestEncounterAction(
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> ResolveComplySuspicionAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveFleeSuspicionAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveAttemptTrapAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveWithdrawTrapAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ResolveDisarmTrapAction(CancellationToken cancellationToken);
    IAsyncEnumerable<string> StartTheftEncounterNarration(
        Guid encounterId,
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> ResolveApologizeTheftEncounterAction(
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<string> ResolveFleeTheftEncounterAction(CancellationToken cancellationToken);
    Task AcknowledgeEvents(Guid flushId);
}

internal sealed class ChatHub(
    GameTurnRunner gameTurnRunner,
    GameClientEventDispatcher eventDispatcher,
    ICommandHandler<PublishSessionStateCommand> publishSessionState,
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    PendingSessionEndRegistry pendingSessionEnds,
    PendingEventAckRegistry pendingEventAcks
) : Hub<IGameClient>, IChatHub
{
    private const string SessionKey = "Session";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId }
        );
        await pendingSessionEnds.Connect(snapshot.Id, snapshot.WorldId, Context.ConnectionAborted);
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
            await pendingSessionEnds.Disconnect(session.SessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task EndSession()
    {
        await pendingSessionEnds.End(Session.SessionId);
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamOpening(Session, cancellationToken);

    public IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken) =>
        gameTurnRunner.StreamChat(Session, message, cancellationToken);

    public IAsyncEnumerable<string> SendWait(
        int hours,
        int minutes,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamWait(Session, hours, minutes, cancellationToken);

    public IAsyncEnumerable<string> SendSitDown(Guid seatId, CancellationToken cancellationToken) =>
        gameTurnRunner.StreamSitDown(Session, seatId, cancellationToken);

    public IAsyncEnumerable<string> SendStandUp(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamStandUp(Session, cancellationToken);

    public IAsyncEnumerable<string> SendSleep(
        int hours,
        int minutes,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamSleep(Session, hours, minutes, cancellationToken);

    public IAsyncEnumerable<string> SendActivateTrigger(
        Guid triggerId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamActivateTrigger(Session, triggerId, cancellationToken);

    public IAsyncEnumerable<string> SendAcceptQuest(
        Guid questId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamAcceptQuest(Session, questId, cancellationToken);

    public IAsyncEnumerable<string> SendDeclineQuest(
        Guid questId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamDeclineQuest(Session, questId, cancellationToken);

    public IAsyncEnumerable<string> SendCompleteQuest(
        Guid questId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamCompleteQuest(Session, questId, cancellationToken);

    public IAsyncEnumerable<string> SendDeliverItem(
        Guid recipientId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamDeliverItem(Session, recipientId, cancellationToken);

    public IAsyncEnumerable<string> SendPurchaseCaravanTicket(
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamPurchaseCaravanTicket(
            Session,
            caravanId,
            destinationLocationId,
            cancellationToken
        );

    public IAsyncEnumerable<string> SendDeclineCaravanTicket(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamDeclineCaravanTicket(Session, cancellationToken);

    public IAsyncEnumerable<string> SendBoardCaravan(
        Guid caravanId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamBoardCaravan(Session, caravanId, cancellationToken);

    public IAsyncEnumerable<string> SendFlee(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamFlee(Session, cancellationToken);

    public IAsyncEnumerable<string> SendRespawn(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamRespawn(Session, cancellationToken);

    public IAsyncEnumerable<string> ResolveAttackEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamHostileEncounterAction(
            Session,
            new AttackEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveFleeEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamHostileEncounterAction(
            Session,
            new FleeEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveIntimidateEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamShakedownEncounterAction(
            Session,
            new IntimidateEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolvePayTollEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamShakedownEncounterAction(
            Session,
            new PayTollEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveFightEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamShakedownEncounterAction(
            Session,
            new FightEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveFleeShakedownEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamShakedownEncounterAction(
            Session,
            new FleeShakedownEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolvePayFineEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamGuardEncounterAction(
            Session,
            new PayFineEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveGoToJailEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamGuardEncounterAction(
            Session,
            new GoToJailEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveResistArrestEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamGuardEncounterAction(
            Session,
            new ResistArrestEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveComplySuspicionAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamSuspicionEncounterAction(
            Session,
            new ComplySuspicionAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveFleeSuspicionAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamSuspicionEncounterAction(
            Session,
            new FleeSuspicionAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveAttemptTrapAction(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamTrapEncounterAction(
            Session,
            new AttemptTrapAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveWithdrawTrapAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamTrapEncounterAction(
            Session,
            new WithdrawTrapAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveDisarmTrapAction(CancellationToken cancellationToken) =>
        gameTurnRunner.StreamTrapEncounterAction(
            Session,
            new DisarmTrapAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> StartTheftEncounterNarration(
        Guid encounterId,
        CancellationToken cancellationToken
    ) => gameTurnRunner.StreamTheftEncounterNarration(Session, encounterId, cancellationToken);

    public IAsyncEnumerable<string> ResolveApologizeTheftEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamTheftEncounterAction(
            Session,
            new ApologizeTheftEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveFleeTheftEncounterAction(
        CancellationToken cancellationToken
    ) =>
        gameTurnRunner.StreamTheftEncounterAction(
            Session,
            new FleeTheftEncounterAction(),
            cancellationToken
        );

    public IAsyncEnumerable<string> ResolveUseAbilityCombatAction(
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken
    ) => ResolveCombatAction(new UseAbilityAction(targetId, abilityName), cancellationToken);

    public IAsyncEnumerable<string> ResolveUseItemCombatAction(
        string itemName,
        CancellationToken cancellationToken
    ) => ResolveCombatAction(new UseItemAction(itemName), cancellationToken);

    private async IAsyncEnumerable<string> ResolveCombatAction(
        PlayerCombatAction action,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (
            var token in gameTurnRunner.StreamCombatAction(Session, action, cancellationToken)
        )
        {
            yield return token;
        }

        await eventDispatcher.FlushAsync(Session.WorldId, cancellationToken);
    }

    public Task AcknowledgeEvents(Guid flushId)
    {
        pendingEventAcks.Acknowledge(flushId);
        return Task.CompletedTask;
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
    IWorldClock worldClock,
    TimeProvider timeProvider,
    IOptionsMonitor<GameSessionOptions> options,
    ILogger<PendingSessionEndRegistry> logger
) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionActivity> _sessions = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    public async Task Connect(
        Guid sessionId,
        Guid worldId,
        CancellationToken cancellationToken = default
    )
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var activity = _sessions.GetOrAdd(sessionId, _ => new SessionActivity(worldId));
            if (activity.WorldId != worldId)
            {
                throw new InvalidOperationException("A session cannot change worlds.");
            }

            if (activity.PendingEnd != null)
            {
                await activity.PendingEnd.CancelAsync();
            }
            activity.PendingEnd = null;
            activity.ConnectionCount++;

            await worldClock.ResumeWorld(worldId, cancellationToken);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task Disconnect(Guid sessionId)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var activity))
            {
                return;
            }

            activity.ConnectionCount--;
            if (activity.ConnectionCount > 0 || activity.PendingEnd != null)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            activity.PendingEnd = cancellation;
            _ = RunAfterDelay(sessionId, activity, cancellation);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task End(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (!_sessions.TryRemove(sessionId, out var activity))
            {
                return;
            }

            if (activity.PendingEnd != null)
            {
                await activity.PendingEnd.CancelAsync();
            }
            await EndSession(sessionId, cancellationToken);
            await PauseWorldIfInactive(activity.WorldId, cancellationToken);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task RunAfterDelay(
        Guid sessionId,
        SessionActivity activity,
        CancellationTokenSource cancellation
    )
    {
        try
        {
            await Task.Delay(
                options.CurrentValue.SessionEndGracePeriod,
                timeProvider,
                cancellation.Token
            );

            await _lifecycleGate.WaitAsync(cancellation.Token);
            try
            {
                if (
                    activity.ConnectionCount != 0
                    || activity.PendingEnd != cancellation
                    || !_sessions.TryRemove(sessionId, out _)
                )
                {
                    return;
                }

                await EndSession(sessionId, cancellation.Token);
                await PauseWorldIfInactive(activity.WorldId, cancellation.Token);
            }
            finally
            {
                _lifecycleGate.Release();
            }
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
            if (activity.PendingEnd == cancellation)
            {
                activity.PendingEnd = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task EndSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var endGameSession = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<EndGameSessionCommand>
        >();
        await endGameSession.Handle(
            new EndGameSessionCommand { SessionId = sessionId },
            cancellationToken
        );
    }

    private async Task PauseWorldIfInactive(Guid worldId, CancellationToken cancellationToken)
    {
        if (_sessions.Values.All(activity => activity.WorldId != worldId))
        {
            await worldClock.PauseWorld(worldId, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var activity in _sessions.Values)
        {
            if (activity.PendingEnd != null)
            {
                await activity.PendingEnd.CancelAsync();
            }
        }

        _lifecycleGate.Dispose();
    }

    private sealed class SessionActivity(Guid worldId)
    {
        public Guid WorldId { get; } = worldId;
        public int ConnectionCount { get; set; }
        public CancellationTokenSource? PendingEnd { get; set; }
    }
}
