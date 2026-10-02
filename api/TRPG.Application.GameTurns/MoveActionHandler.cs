namespace TRPG.Application.GameTurns;

internal class MoveActionHandler(GameActionRunner actionRunner, MoveTurnResolver resolver)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid connectorId,
        CancellationToken cancellationToken = default
    ) =>
        actionRunner.Run(
            session,
            ct => resolver.Resolve(session, connectorId, ct),
            cancellationToken
        );
}
