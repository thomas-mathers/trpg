using TRPG.Application.Combat;
using TRPG.Application.Encounters;

namespace TRPG.Application.GameTurns;

public class GameTurnRunner
{
    private readonly StreamOpeningTurnHandler _streamOpeningTurn;
    private readonly StreamChatTurnHandler _streamChatTurn;
    private readonly StreamWaitTurnHandler _streamWaitTurn;
    private readonly StreamSleepTurnHandler _streamSleepTurn;
    private readonly StreamFleeTurnHandler _streamFleeTurn;
    private readonly StreamRespawnTurnHandler _streamRespawnTurn;
    private readonly StreamHostileEncounterActionTurnHandler _streamHostileEncounterActionTurn;
    private readonly StreamGuardEncounterActionTurnHandler _streamGuardEncounterActionTurn;
    private readonly StreamSuspicionEncounterActionTurnHandler _streamSuspicionEncounterActionTurn;
    private readonly StreamTheftEncounterNarrationTurnHandler _streamTheftEncounterNarrationTurn;
    private readonly StreamTheftEncounterActionTurnHandler _streamTheftEncounterActionTurn;
    private readonly StreamCombatActionTurnHandler _streamCombatActionTurn;

    internal GameTurnRunner(
        StreamOpeningTurnHandler streamOpeningTurn,
        StreamChatTurnHandler streamChatTurn,
        StreamWaitTurnHandler streamWaitTurn,
        StreamSleepTurnHandler streamSleepTurn,
        StreamFleeTurnHandler streamFleeTurn,
        StreamRespawnTurnHandler streamRespawnTurn,
        StreamHostileEncounterActionTurnHandler streamHostileEncounterActionTurn,
        StreamGuardEncounterActionTurnHandler streamGuardEncounterActionTurn,
        StreamSuspicionEncounterActionTurnHandler streamSuspicionEncounterActionTurn,
        StreamTheftEncounterNarrationTurnHandler streamTheftEncounterNarrationTurn,
        StreamTheftEncounterActionTurnHandler streamTheftEncounterActionTurn,
        StreamCombatActionTurnHandler streamCombatActionTurn
    )
    {
        _streamOpeningTurn = streamOpeningTurn;
        _streamChatTurn = streamChatTurn;
        _streamWaitTurn = streamWaitTurn;
        _streamSleepTurn = streamSleepTurn;
        _streamFleeTurn = streamFleeTurn;
        _streamRespawnTurn = streamRespawnTurn;
        _streamHostileEncounterActionTurn = streamHostileEncounterActionTurn;
        _streamGuardEncounterActionTurn = streamGuardEncounterActionTurn;
        _streamSuspicionEncounterActionTurn = streamSuspicionEncounterActionTurn;
        _streamTheftEncounterNarrationTurn = streamTheftEncounterNarrationTurn;
        _streamTheftEncounterActionTurn = streamTheftEncounterActionTurn;
        _streamCombatActionTurn = streamCombatActionTurn;
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
}
