namespace TRPG.Application.Combat;

public interface ICombatStatusPublisher
{
    Task PublishCombatStatus(
        Guid worldId,
        IReadOnlyList<Combatant> combatants,
        CancellationToken cancellationToken = default
    );
}
