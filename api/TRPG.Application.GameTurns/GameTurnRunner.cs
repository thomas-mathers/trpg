using TRPG.Application.Combat;
using TRPG.Application.Encounters;

namespace TRPG.Application.GameTurns;

public class GameTurnRunner
{
    private readonly StreamOpeningTurnHandler _streamOpeningTurn;
    private readonly StreamChatTurnHandler _streamChatTurn;
    private readonly StreamWaitTurnHandler _streamWaitTurn;
    private readonly StreamSleepTurnHandler _streamSleepTurn;
    private readonly StreamActivateTriggerTurnHandler _streamActivateTriggerTurn;
    private readonly StreamAcceptQuestTurnHandler _streamAcceptQuestTurn;
    private readonly StreamDeclineQuestTurnHandler _streamDeclineQuestTurn;
    private readonly StreamCompleteQuestTurnHandler _streamCompleteQuestTurn;
    private readonly StreamDeliverItemTurnHandler _streamDeliverItemTurn;
    private readonly StreamFleeTurnHandler _streamFleeTurn;
    private readonly StreamRespawnTurnHandler _streamRespawnTurn;
    private readonly StreamHostileEncounterActionTurnHandler _streamHostileEncounterActionTurn;
    private readonly StreamShakedownEncounterActionTurnHandler _streamShakedownEncounterActionTurn;
    private readonly StreamGuardEncounterActionTurnHandler _streamGuardEncounterActionTurn;
    private readonly StreamSuspicionEncounterActionTurnHandler _streamSuspicionEncounterActionTurn;
    private readonly StreamTrapEncounterActionTurnHandler _streamTrapEncounterActionTurn;
    private readonly StreamTheftEncounterNarrationTurnHandler _streamTheftEncounterNarrationTurn;
    private readonly StreamTheftEncounterActionTurnHandler _streamTheftEncounterActionTurn;
    private readonly StreamCombatActionTurnHandler _streamCombatActionTurn;
    private readonly StreamPurchaseCaravanTicketTurnHandler _streamPurchaseCaravanTicketTurn;
    private readonly StreamDeclineCaravanTicketTurnHandler _streamDeclineCaravanTicketTurn;
    private readonly StreamBoardCaravanTurnHandler _streamBoardCaravanTurn;

    internal GameTurnRunner(
        StreamOpeningTurnHandler streamOpeningTurn,
        StreamChatTurnHandler streamChatTurn,
        StreamWaitTurnHandler streamWaitTurn,
        StreamSleepTurnHandler streamSleepTurn,
        StreamActivateTriggerTurnHandler streamActivateTriggerTurn,
        StreamAcceptQuestTurnHandler streamAcceptQuestTurn,
        StreamDeclineQuestTurnHandler streamDeclineQuestTurn,
        StreamCompleteQuestTurnHandler streamCompleteQuestTurn,
        StreamDeliverItemTurnHandler streamDeliverItemTurn,
        StreamFleeTurnHandler streamFleeTurn,
        StreamRespawnTurnHandler streamRespawnTurn,
        StreamHostileEncounterActionTurnHandler streamHostileEncounterActionTurn,
        StreamShakedownEncounterActionTurnHandler streamShakedownEncounterActionTurn,
        StreamGuardEncounterActionTurnHandler streamGuardEncounterActionTurn,
        StreamSuspicionEncounterActionTurnHandler streamSuspicionEncounterActionTurn,
        StreamTrapEncounterActionTurnHandler streamTrapEncounterActionTurn,
        StreamTheftEncounterNarrationTurnHandler streamTheftEncounterNarrationTurn,
        StreamTheftEncounterActionTurnHandler streamTheftEncounterActionTurn,
        StreamCombatActionTurnHandler streamCombatActionTurn,
        StreamPurchaseCaravanTicketTurnHandler streamPurchaseCaravanTicketTurn,
        StreamDeclineCaravanTicketTurnHandler streamDeclineCaravanTicketTurn,
        StreamBoardCaravanTurnHandler streamBoardCaravanTurn
    )
    {
        _streamOpeningTurn = streamOpeningTurn;
        _streamChatTurn = streamChatTurn;
        _streamWaitTurn = streamWaitTurn;
        _streamSleepTurn = streamSleepTurn;
        _streamActivateTriggerTurn = streamActivateTriggerTurn;
        _streamAcceptQuestTurn = streamAcceptQuestTurn;
        _streamDeclineQuestTurn = streamDeclineQuestTurn;
        _streamCompleteQuestTurn = streamCompleteQuestTurn;
        _streamDeliverItemTurn = streamDeliverItemTurn;
        _streamFleeTurn = streamFleeTurn;
        _streamRespawnTurn = streamRespawnTurn;
        _streamHostileEncounterActionTurn = streamHostileEncounterActionTurn;
        _streamShakedownEncounterActionTurn = streamShakedownEncounterActionTurn;
        _streamGuardEncounterActionTurn = streamGuardEncounterActionTurn;
        _streamSuspicionEncounterActionTurn = streamSuspicionEncounterActionTurn;
        _streamTrapEncounterActionTurn = streamTrapEncounterActionTurn;
        _streamTheftEncounterNarrationTurn = streamTheftEncounterNarrationTurn;
        _streamTheftEncounterActionTurn = streamTheftEncounterActionTurn;
        _streamCombatActionTurn = streamCombatActionTurn;
        _streamPurchaseCaravanTicketTurn = streamPurchaseCaravanTicketTurn;
        _streamDeclineCaravanTicketTurn = streamDeclineCaravanTicketTurn;
        _streamBoardCaravanTurn = streamBoardCaravanTurn;
    }

    public IAsyncEnumerable<string> StreamOpening(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _streamOpeningTurn.Handle(session, cancellationToken);

    public IAsyncEnumerable<string> StreamChat(
        GameTurnSession session,
        string message,
        CancellationToken cancellationToken = default
    ) => _streamChatTurn.Handle(session, message, cancellationToken);

    public IAsyncEnumerable<string> StreamWait(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken = default
    ) => _streamWaitTurn.Handle(session, hours, minutes, cancellationToken);

    public IAsyncEnumerable<string> StreamSleep(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken = default
    ) => _streamSleepTurn.Handle(session, hours, minutes, cancellationToken);

    public IAsyncEnumerable<string> StreamActivateTrigger(
        GameTurnSession session,
        Guid triggerId,
        CancellationToken cancellationToken = default
    ) => _streamActivateTriggerTurn.Handle(session, triggerId, cancellationToken);

    public IAsyncEnumerable<string> StreamAcceptQuest(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => _streamAcceptQuestTurn.Handle(session, questId, cancellationToken);

    public IAsyncEnumerable<string> StreamDeclineQuest(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => _streamDeclineQuestTurn.Handle(session, questId, cancellationToken);

    public IAsyncEnumerable<string> StreamCompleteQuest(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => _streamCompleteQuestTurn.Handle(session, questId, cancellationToken);

    public IAsyncEnumerable<string> StreamDeliverItem(
        GameTurnSession session,
        Guid recipientId,
        CancellationToken cancellationToken = default
    ) => _streamDeliverItemTurn.Handle(session, recipientId, cancellationToken);

    public IAsyncEnumerable<string> StreamFlee(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _streamFleeTurn.Handle(session, cancellationToken);

    public IAsyncEnumerable<string> StreamRespawn(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _streamRespawnTurn.Handle(session, cancellationToken);

    public IAsyncEnumerable<string> StreamHostileEncounterAction(
        GameTurnSession session,
        HostileEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamHostileEncounterActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamShakedownEncounterAction(
        GameTurnSession session,
        ShakedownEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamShakedownEncounterActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamGuardEncounterAction(
        GameTurnSession session,
        GuardEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamGuardEncounterActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamSuspicionEncounterAction(
        GameTurnSession session,
        SuspicionEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamSuspicionEncounterActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamTrapEncounterAction(
        GameTurnSession session,
        TrapEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamTrapEncounterActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamTheftEncounterNarration(
        GameTurnSession session,
        Guid encounterId,
        CancellationToken cancellationToken = default
    ) => _streamTheftEncounterNarrationTurn.Handle(session, encounterId, cancellationToken);

    public IAsyncEnumerable<string> StreamTheftEncounterAction(
        GameTurnSession session,
        TheftEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamTheftEncounterActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamCombatAction(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    ) => _streamCombatActionTurn.Handle(session, action, cancellationToken);

    public IAsyncEnumerable<string> StreamPurchaseCaravanTicket(
        GameTurnSession session,
        Guid caravanId,
        Guid destinationLocationId,
        CancellationToken cancellationToken = default
    ) =>
        _streamPurchaseCaravanTicketTurn.Handle(
            session,
            caravanId,
            destinationLocationId,
            cancellationToken
        );

    public IAsyncEnumerable<string> StreamDeclineCaravanTicket(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _streamDeclineCaravanTicketTurn.Handle(session, cancellationToken);

    public IAsyncEnumerable<string> StreamBoardCaravan(
        GameTurnSession session,
        Guid caravanId,
        CancellationToken cancellationToken = default
    ) => _streamBoardCaravanTurn.Handle(session, caravanId, cancellationToken);
}
