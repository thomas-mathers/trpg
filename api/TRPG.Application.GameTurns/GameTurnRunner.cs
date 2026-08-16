using TRPG.Contracts.Combat.Requests;
using TRPG.Contracts.Encounters.Requests;

namespace TRPG.Application.GameTurns;

public class GameTurnRunner
{
    private readonly StreamOpeningTurnHandler _streamOpeningTurn;
    private readonly StreamChatTurnHandler _streamChatTurn;
    private readonly StreamWaitTurnHandler _streamWaitTurn;
    private readonly StreamFleeTurnHandler _streamFleeTurn;
    private readonly StreamEncounterActionTurnHandler _streamEncounterActionTurn;
    private readonly ResolveCombatActionHandler _resolveCombatAction;

    internal GameTurnRunner(
        StreamOpeningTurnHandler streamOpeningTurn,
        StreamChatTurnHandler streamChatTurn,
        StreamWaitTurnHandler streamWaitTurn,
        StreamFleeTurnHandler streamFleeTurn,
        StreamEncounterActionTurnHandler streamEncounterActionTurn,
        ResolveCombatActionHandler resolveCombatAction
    )
    {
        _streamOpeningTurn = streamOpeningTurn;
        _streamChatTurn = streamChatTurn;
        _streamWaitTurn = streamWaitTurn;
        _streamFleeTurn = streamFleeTurn;
        _streamEncounterActionTurn = streamEncounterActionTurn;
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
        CancellationToken cancellationToken = default
    ) => _streamWaitTurn.Handle(session, hours, cancellationToken);

    public IAsyncEnumerable<string> StreamFlee(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => _streamFleeTurn.Handle(session, cancellationToken);

    public IAsyncEnumerable<string> StreamEncounterAction(
        GameTurnSession session,
        PlayerEncounterAction action,
        CancellationToken cancellationToken = default
    ) => _streamEncounterActionTurn.Handle(session, action, cancellationToken);

    public Task ResolveCombatAction(
        GameTurnSession session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    ) => _resolveCombatAction.Handle(session, action, cancellationToken);
}
