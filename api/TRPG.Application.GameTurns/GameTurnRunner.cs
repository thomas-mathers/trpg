using TRPG.Application.Combat;
using TRPG.Application.Encounters;

namespace TRPG.Application.GameTurns;

public class GameTurnRunner
{
    private readonly StreamOpeningTurnHandler _streamOpeningTurn;
    private readonly StreamChatTurnHandler _streamChatTurn;
    private readonly StreamWaitTurnHandler _streamWaitTurn;
    private readonly StreamFleeTurnHandler _streamFleeTurn;
    private readonly StreamHostileEncounterActionTurnHandler _streamHostileEncounterActionTurn;
    private readonly ResolveCombatActionHandler _resolveCombatAction;

    internal GameTurnRunner(
        StreamOpeningTurnHandler streamOpeningTurn,
        StreamChatTurnHandler streamChatTurn,
        StreamWaitTurnHandler streamWaitTurn,
        StreamFleeTurnHandler streamFleeTurn,
        StreamHostileEncounterActionTurnHandler streamHostileEncounterActionTurn,
        ResolveCombatActionHandler resolveCombatAction
    )
    {
        _streamOpeningTurn = streamOpeningTurn;
        _streamChatTurn = streamChatTurn;
        _streamWaitTurn = streamWaitTurn;
        _streamFleeTurn = streamFleeTurn;
        _streamHostileEncounterActionTurn = streamHostileEncounterActionTurn;
        _resolveCombatAction = resolveCombatAction;
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

    public IAsyncEnumerable<string> StreamFlee(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _streamFleeTurn.Handle(session, cancellationToken);

    public IAsyncEnumerable<string> StreamHostileEncounterAction(
        GameTurnSession session,
        HostileEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamHostileEncounterActionTurn.Handle(session, action, cancellationToken);

    public Task ResolveCombatAction(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    ) => _resolveCombatAction.Handle(session, action, cancellationToken);
}
