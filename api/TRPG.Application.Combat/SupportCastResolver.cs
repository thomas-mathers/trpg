using TRPG.Application.Abilities;
using TRPG.Domain;

namespace TRPG.Application.Combat;

internal static class SupportCastResolver
{
    public static PlayerCombatActionResolverResult Resolve(
        IReadOnlyList<Combatant> combatants,
        UseAbilityAction action,
        GameInstant now
    )
    {
        var player = combatants.SingleOrDefault(c => c.IsPlayer);
        if (player is null)
        {
            return PlayerCombatActionResolverResult.Failure("No active player found.");
        }

        var ability = player.Abilities.FirstOrDefault(x => x.Name == action.AbilityName);
        if (ability is null)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability {action.AbilityName} not found"
            );
        }

        if (ability is not SupportAbility support)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability {action.AbilityName} is not a support ability"
            );
        }

        var target = combatants.FirstOrDefault(x => x.CreatureId == action.TargetId);
        if (target is null)
        {
            return PlayerCombatActionResolverResult.Failure($"Target {action.TargetId} not found");
        }

        if (!target.IsAlive)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Target {action.TargetId} is already dead"
            );
        }

        var failure = AbilityUsability.FirstFailure(player, ability, now);
        if (failure is not null)
        {
            return PlayerCombatActionResolverResult.Failure(failure);
        }

        return PlayerCombatActionResolverResult.Success(
            new ResolvedUseAbilityAction(ability, ResolveTargets(support, player, target))
        );
    }

    private static IReadOnlyList<Combatant> ResolveTargets(
        SupportAbility ability,
        Combatant player,
        Combatant target
    ) =>
        ability.TargetType switch
        {
            TargetType.Aoe => GetAllies(player),
            TargetType.Self => [player],
            _ => [target],
        };

    private static IReadOnlyList<Combatant> GetAllies(Combatant player) => [player];
}
