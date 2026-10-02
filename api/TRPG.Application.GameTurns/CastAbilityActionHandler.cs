namespace TRPG.Application.GameTurns;

internal class CastAbilityActionHandler(
    GameActionRunner actionRunner,
    CastAbilityTurnResolver resolver
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken = default
    ) =>
        actionRunner.Run(
            session,
            ct => resolver.Resolve(session, targetId, abilityName, ct),
            cancellationToken
        );
}
