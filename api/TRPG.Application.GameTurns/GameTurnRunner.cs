using TRPG.Application.Combat;
using TRPG.Application.Encounters;

namespace TRPG.Application.GameTurns;

public class GameTurnRunner
{
    private readonly StreamChatTurnHandler _streamChatTurn;
    private readonly WaitActionHandler _waitActionHandler;
    private readonly SitDownActionHandler _sitDownActionHandler;
    private readonly StandUpActionHandler _standUpActionHandler;
    private readonly SleepActionHandler _sleepActionHandler;
    private readonly ActivateTriggerActionHandler _activateTriggerActionHandler;
    private readonly AcceptQuestActionHandler _acceptQuestActionHandler;
    private readonly CompleteQuestActionHandler _completeQuestActionHandler;
    private readonly DeliverItemActionHandler _deliverItemActionHandler;
    private readonly FleeActionHandler _fleeActionHandler;
    private readonly RespawnActionHandler _respawnActionHandler;
    private readonly HostileEncounterActionHandler _hostileEncounterActionHandler;
    private readonly ShakedownEncounterActionHandler _shakedownEncounterActionHandler;
    private readonly GuardEncounterActionHandler _guardEncounterActionHandler;
    private readonly SuspicionEncounterActionHandler _suspicionEncounterActionHandler;
    private readonly TrapEncounterActionHandler _trapEncounterActionHandler;
    private readonly StartTheftEncounterActionHandler _startTheftEncounterActionHandler;
    private readonly TheftEncounterActionHandler _theftEncounterActionHandler;
    private readonly CombatActionHandler _combatActionHandler;
    private readonly CastAbilityActionHandler _castAbilityActionHandler;
    private readonly PurchaseCaravanTicketActionHandler _purchaseCaravanTicketActionHandler;
    private readonly BoardCaravanActionHandler _boardCaravanActionHandler;
    private readonly MoveActionHandler _moveActionHandler;

    internal GameTurnRunner(
        StreamChatTurnHandler streamChatTurn,
        WaitActionHandler waitActionHandler,
        SitDownActionHandler sitDownActionHandler,
        StandUpActionHandler standUpActionHandler,
        SleepActionHandler sleepActionHandler,
        ActivateTriggerActionHandler activateTriggerActionHandler,
        AcceptQuestActionHandler acceptQuestActionHandler,
        CompleteQuestActionHandler completeQuestActionHandler,
        DeliverItemActionHandler deliverItemActionHandler,
        FleeActionHandler fleeActionHandler,
        RespawnActionHandler respawnActionHandler,
        HostileEncounterActionHandler hostileEncounterActionHandler,
        ShakedownEncounterActionHandler shakedownEncounterActionHandler,
        GuardEncounterActionHandler guardEncounterActionHandler,
        SuspicionEncounterActionHandler suspicionEncounterActionHandler,
        TrapEncounterActionHandler trapEncounterActionHandler,
        StartTheftEncounterActionHandler startTheftEncounterActionHandler,
        TheftEncounterActionHandler theftEncounterActionHandler,
        CombatActionHandler combatActionHandler,
        CastAbilityActionHandler castAbilityActionHandler,
        PurchaseCaravanTicketActionHandler purchaseCaravanTicketActionHandler,
        BoardCaravanActionHandler boardCaravanActionHandler,
        MoveActionHandler moveActionHandler
    )
    {
        _streamChatTurn = streamChatTurn;
        _waitActionHandler = waitActionHandler;
        _sitDownActionHandler = sitDownActionHandler;
        _standUpActionHandler = standUpActionHandler;
        _sleepActionHandler = sleepActionHandler;
        _activateTriggerActionHandler = activateTriggerActionHandler;
        _acceptQuestActionHandler = acceptQuestActionHandler;
        _completeQuestActionHandler = completeQuestActionHandler;
        _deliverItemActionHandler = deliverItemActionHandler;
        _fleeActionHandler = fleeActionHandler;
        _respawnActionHandler = respawnActionHandler;
        _hostileEncounterActionHandler = hostileEncounterActionHandler;
        _shakedownEncounterActionHandler = shakedownEncounterActionHandler;
        _guardEncounterActionHandler = guardEncounterActionHandler;
        _suspicionEncounterActionHandler = suspicionEncounterActionHandler;
        _trapEncounterActionHandler = trapEncounterActionHandler;
        _startTheftEncounterActionHandler = startTheftEncounterActionHandler;
        _theftEncounterActionHandler = theftEncounterActionHandler;
        _combatActionHandler = combatActionHandler;
        _castAbilityActionHandler = castAbilityActionHandler;
        _purchaseCaravanTicketActionHandler = purchaseCaravanTicketActionHandler;
        _boardCaravanActionHandler = boardCaravanActionHandler;
        _moveActionHandler = moveActionHandler;
    }

    public IAsyncEnumerable<string> StreamChat(
        GameTurnSession session,
        string message,
        CancellationToken cancellationToken = default
    ) => _streamChatTurn.Handle(session, message, cancellationToken);

    public Task<ActionOutcome> Wait(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken = default
    ) => _waitActionHandler.Handle(session, hours, minutes, cancellationToken);

    public Task<ActionOutcome> SitDown(
        GameTurnSession session,
        Guid seatId,
        CancellationToken cancellationToken = default
    ) => _sitDownActionHandler.Handle(session, seatId, cancellationToken);

    public Task<ActionOutcome> StandUp(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _standUpActionHandler.Handle(session, cancellationToken);

    public Task<ActionOutcome> Sleep(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken = default
    ) => _sleepActionHandler.Handle(session, hours, minutes, cancellationToken);

    public Task<ActionOutcome> ActivateTrigger(
        GameTurnSession session,
        Guid triggerId,
        CancellationToken cancellationToken = default
    ) => _activateTriggerActionHandler.Handle(session, triggerId, cancellationToken);

    public Task<ActionOutcome> AcceptQuest(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => _acceptQuestActionHandler.Handle(session, questId, cancellationToken);

    public Task<ActionOutcome> CompleteQuest(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => _completeQuestActionHandler.Handle(session, questId, cancellationToken);

    public Task<ActionOutcome> DeliverItem(
        GameTurnSession session,
        Guid recipientId,
        CancellationToken cancellationToken = default
    ) => _deliverItemActionHandler.Handle(session, recipientId, cancellationToken);

    public Task<ActionOutcome> Flee(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _fleeActionHandler.Handle(session, cancellationToken);

    public Task<ActionOutcome> Respawn(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _respawnActionHandler.Handle(session, cancellationToken);

    public Task<ActionOutcome> HostileEncounterAction(
        GameTurnSession session,
        HostileEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _hostileEncounterActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> ShakedownEncounterAction(
        GameTurnSession session,
        ShakedownEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _shakedownEncounterActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> GuardEncounterAction(
        GameTurnSession session,
        GuardEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _guardEncounterActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> SuspicionEncounterAction(
        GameTurnSession session,
        SuspicionEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _suspicionEncounterActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> TrapEncounterAction(
        GameTurnSession session,
        TrapEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _trapEncounterActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> StartTheftEncounter(
        GameTurnSession session,
        Guid encounterId,
        CancellationToken cancellationToken = default
    ) => _startTheftEncounterActionHandler.Handle(session, encounterId, cancellationToken);

    public Task<ActionOutcome> TheftEncounterAction(
        GameTurnSession session,
        TheftEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _theftEncounterActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> CombatAction(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    ) => _combatActionHandler.Handle(session, action, cancellationToken);

    public Task<ActionOutcome> CastAbility(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken = default
    ) => _castAbilityActionHandler.Handle(session, targetId, abilityName, cancellationToken);

    public Task<ActionOutcome> PurchaseCaravanTicket(
        GameTurnSession session,
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken = default
    ) =>
        _purchaseCaravanTicketActionHandler.Handle(
            session,
            caravanId,
            destinationLocationId,
            cancellationToken
        );

    public Task<ActionOutcome> BoardCaravan(
        GameTurnSession session,
        Guid caravanId,
        CancellationToken cancellationToken = default
    ) => _boardCaravanActionHandler.Handle(session, caravanId, cancellationToken);

    public Task<ActionOutcome> Move(
        GameTurnSession session,
        Guid connectorId,
        CancellationToken cancellationToken = default
    ) => _moveActionHandler.Handle(session, connectorId, cancellationToken);
}
