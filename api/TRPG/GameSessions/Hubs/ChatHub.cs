using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Domain.Models;
using TRPG.GameSessions.Commands;
using TRPG.GameSessions.Responses;
using TypedSignalR.Client;

namespace TRPG.GameSessions.Hubs;

[Hub]
public interface IChatHub
{
    Task EndSession();
    IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken);
    Task<ActionResult> SendWait(int hours, int minutes);
    Task<ActionResult> SendSitDown(Guid seatId);
    Task<ActionResult> SendStandUp();
    Task<ActionResult> SendSleep(int hours, int minutes);
    Task<ActionResult> SendActivateTrigger(Guid triggerId);
    Task<ActionResult> SendAcceptQuest(Guid questId);
    Task<ActionResult> SendCompleteQuest(Guid questId);
    Task<ActionResult> SendDeliverItem(Guid recipientId);
    Task<ActionResult> SendMove(Guid connectorId);
    Task SendMovementInput(PlayerMovementInput input);
    Task<ActionResult> SendFlee();
    Task<ActionResult> SendRespawn();
    Task<ActionResult> SendCastAbility(Guid targetId, string abilityName);
    Task<ActionResult> ResolveUseAbilityCombatAction(Guid targetId, string abilityName);
    Task<ActionResult> ResolveUseItemCombatAction(string itemName);
    Task<ActionResult> ResolveAttackEncounterAction();
    Task<ActionResult> ResolveFleeEncounterAction();
    Task<ActionResult> ResolveIntimidateEncounterAction();
    Task<ActionResult> ResolvePayTollEncounterAction();
    Task<ActionResult> ResolveFightEncounterAction();
    Task<ActionResult> ResolveFleeShakedownEncounterAction();
    Task<ActionResult> ResolvePayFineEncounterAction();
    Task<ActionResult> ResolveGoToJailEncounterAction();
    Task<ActionResult> ResolveResistArrestEncounterAction();
    Task<ActionResult> ResolveComplySuspicionAction();
    Task<ActionResult> ResolveFleeSuspicionAction();
    Task<ActionResult> ResolveAttemptTrapAction();
    Task<ActionResult> ResolveWithdrawTrapAction();
    Task<ActionResult> ResolveDisarmTrapAction();
    Task<ActionResult> StartTheftEncounter(Guid encounterId);
    Task<ActionResult> ResolveApologizeTheftEncounterAction();
    Task<ActionResult> ResolveFleeTheftEncounterAction();
}

internal sealed class ChatHub(
    GameTurnRunner gameTurnRunner,
    ICommandHandler<PublishSessionStateCommand> publishSessionState,
    ICommandHandler<FlushPlayerPoseCommand> flushPlayerPose,
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    PendingSessionEndRegistry pendingSessionEnds,
    WorldSimulationCoordinator simulation,
    TimeProvider timeProvider
) : Hub<IGameClient>, IChatHub
{
    private const string SessionKey = "Session";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId }
        );
        if (simulation.IsUnavailable(snapshot.WorldId))
        {
            throw new HubException("This world failed to load and is unavailable.");
        }

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
            try
            {
                await flushPlayerPose.Handle(
                    new FlushPlayerPoseCommand { PlayerId = session.PlayerId },
                    CancellationToken.None
                );
            }
            finally
            {
                await pendingSessionEnds.Disconnect(session.SessionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task EndSession()
    {
        await pendingSessionEnds.End(Session.SessionId);
    }

    public IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken) =>
        gameTurnRunner.StreamChat(Session, message, cancellationToken);

    public Task<ActionResult> SendWait(int hours, int minutes) =>
        Result(gameTurnRunner.Wait(Session, hours, minutes, Context.ConnectionAborted));

    public Task<ActionResult> SendSitDown(Guid seatId) =>
        Result(gameTurnRunner.SitDown(Session, seatId, Context.ConnectionAborted));

    public Task<ActionResult> SendStandUp() =>
        Result(gameTurnRunner.StandUp(Session, Context.ConnectionAborted));

    public Task<ActionResult> SendSleep(int hours, int minutes) =>
        Result(gameTurnRunner.Sleep(Session, hours, minutes, Context.ConnectionAborted));

    public Task<ActionResult> SendActivateTrigger(Guid triggerId) =>
        Result(gameTurnRunner.ActivateTrigger(Session, triggerId, Context.ConnectionAborted));

    public Task<ActionResult> SendAcceptQuest(Guid questId) =>
        Result(gameTurnRunner.AcceptQuest(Session, questId, Context.ConnectionAborted));

    public Task<ActionResult> SendCompleteQuest(Guid questId) =>
        Result(gameTurnRunner.CompleteQuest(Session, questId, Context.ConnectionAborted));

    public Task<ActionResult> SendDeliverItem(Guid recipientId) =>
        Result(gameTurnRunner.DeliverItem(Session, recipientId, Context.ConnectionAborted));

    public Task<ActionResult> SendMove(Guid connectorId) =>
        Result(gameTurnRunner.Move(Session, connectorId, Context.ConnectionAborted));

    public Task SendMovementInput(PlayerMovementInput input)
    {
        simulation.Post(
            Session.WorldId,
            new PlayerInput(
                Session.PlayerId,
                input.LocationId,
                new MovementInput(input.Forward, input.Strafe, input.Heading),
                new Point(input.X, input.Y),
                timeProvider.GetUtcNow()
            )
        );

        return Task.CompletedTask;
    }

    public Task<ActionResult> SendFlee() =>
        Result(gameTurnRunner.Flee(Session, Context.ConnectionAborted));

    public Task<ActionResult> SendRespawn() =>
        Result(gameTurnRunner.Respawn(Session, Context.ConnectionAborted));

    public Task<ActionResult> SendCastAbility(Guid targetId, string abilityName) =>
        Result(
            gameTurnRunner.CastAbility(Session, targetId, abilityName, Context.ConnectionAborted)
        );

    public Task<ActionResult> ResolveAttackEncounterAction() =>
        Result(
            gameTurnRunner.HostileEncounterAction(
                Session,
                new AttackEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveFleeEncounterAction() =>
        Result(
            gameTurnRunner.HostileEncounterAction(
                Session,
                new FleeEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveIntimidateEncounterAction() =>
        Result(
            gameTurnRunner.ShakedownEncounterAction(
                Session,
                new IntimidateEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolvePayTollEncounterAction() =>
        Result(
            gameTurnRunner.ShakedownEncounterAction(
                Session,
                new PayTollEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveFightEncounterAction() =>
        Result(
            gameTurnRunner.ShakedownEncounterAction(
                Session,
                new FightEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveFleeShakedownEncounterAction() =>
        Result(
            gameTurnRunner.ShakedownEncounterAction(
                Session,
                new FleeShakedownEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolvePayFineEncounterAction() =>
        Result(
            gameTurnRunner.GuardEncounterAction(
                Session,
                new PayFineEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveGoToJailEncounterAction() =>
        Result(
            gameTurnRunner.GuardEncounterAction(
                Session,
                new GoToJailEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveResistArrestEncounterAction() =>
        Result(
            gameTurnRunner.GuardEncounterAction(
                Session,
                new ResistArrestEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveComplySuspicionAction() =>
        Result(
            gameTurnRunner.SuspicionEncounterAction(
                Session,
                new ComplySuspicionAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveFleeSuspicionAction() =>
        Result(
            gameTurnRunner.SuspicionEncounterAction(
                Session,
                new FleeSuspicionAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveAttemptTrapAction() =>
        Result(
            gameTurnRunner.TrapEncounterAction(
                Session,
                new AttemptTrapAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveWithdrawTrapAction() =>
        Result(
            gameTurnRunner.TrapEncounterAction(
                Session,
                new WithdrawTrapAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveDisarmTrapAction() =>
        Result(
            gameTurnRunner.TrapEncounterAction(
                Session,
                new DisarmTrapAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> StartTheftEncounter(Guid encounterId) =>
        Result(gameTurnRunner.StartTheftEncounter(Session, encounterId, Context.ConnectionAborted));

    public Task<ActionResult> ResolveApologizeTheftEncounterAction() =>
        Result(
            gameTurnRunner.TheftEncounterAction(
                Session,
                new ApologizeTheftEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveFleeTheftEncounterAction() =>
        Result(
            gameTurnRunner.TheftEncounterAction(
                Session,
                new FleeTheftEncounterAction(),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveUseAbilityCombatAction(Guid targetId, string abilityName) =>
        Result(
            gameTurnRunner.CombatAction(
                Session,
                new UseAbilityAction(targetId, abilityName),
                Context.ConnectionAborted
            )
        );

    public Task<ActionResult> ResolveUseItemCombatAction(string itemName) =>
        Result(
            gameTurnRunner.CombatAction(
                Session,
                new UseItemAction(itemName),
                Context.ConnectionAborted
            )
        );

    private static async Task<ActionResult> Result(Task<ActionOutcome> outcome) =>
        ActionResult.From(await outcome);

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

            await ResumeWorld(sessionId, activity, cancellationToken);

            if (activity.PendingEnd != null)
            {
                await activity.PendingEnd.CancelAsync();
            }
            activity.PendingEnd = null;
            activity.ConnectionCount++;
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
            try
            {
                await EndSession(sessionId, cancellationToken);
            }
            catch (EntityNotFoundException)
            {
                // Already ended some other way; the world still needs its pause.
            }
            finally
            {
                await PauseWorldIfInactive(activity.WorldId, CancellationToken.None);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ResumeWorld(
        Guid sessionId,
        SessionActivity activity,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await worldClock.ResumeWorld(activity.WorldId, cancellationToken);
        }
        catch
        {
            if (activity.ConnectionCount == 0 && activity.PendingEnd == null)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            throw;
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

                try
                {
                    await EndSession(sessionId, cancellation.Token);
                }
                finally
                {
                    await PauseWorldIfInactive(activity.WorldId, CancellationToken.None);
                }
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
