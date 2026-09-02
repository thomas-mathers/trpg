using TRPG.Application.Abilities;

namespace TRPG.Application.Combat;

public abstract record ResolvedCombatAction;

internal sealed record ResolvedUseAbilityAction(Ability Ability, IReadOnlyList<Combatant> Targets)
    : ResolvedCombatAction;

internal sealed record ResolvedUseItemAction(ConsumableItemSnapshot Item) : ResolvedCombatAction;

public class PlayerCombatActionResolverResult
{
    public ResolvedCombatAction? Result { get; }
    public string? ErrorMessage { get; }
    public bool IsError => ErrorMessage != null;

    private PlayerCombatActionResolverResult(
        ResolvedCombatAction? result,
        string? errorMessage = null
    )
    {
        Result = result;
        ErrorMessage = errorMessage;
    }

    public static PlayerCombatActionResolverResult Success(ResolvedCombatAction result) =>
        new(result);

    public static PlayerCombatActionResolverResult Failure(string? errorMessage = null) =>
        new(null, errorMessage);
}

public class PlayerCombatActionResolver(IReadOnlyList<Combatant> combatants)
{
    public PlayerCombatActionResolverResult Resolve(PlayerCombatAction playerAction) =>
        playerAction switch
        {
            UseAbilityAction useAbility => ResolveUseAbilityAction(combatants, useAbility),
            UseItemAction useItem => ResolveUseItemAction(combatants, useItem),
            _ => throw new InvalidOperationException("Unrecognized action."),
        };

    private static PlayerCombatActionResolverResult ResolveUseAbilityAction(
        IReadOnlyList<Combatant> combatants,
        UseAbilityAction useAbilityAction
    )
    {
        var player = combatants.SingleOrDefault(c => c.IsPlayer);

        if (player is null)
        {
            return PlayerCombatActionResolverResult.Failure("No active player found in combat.");
        }

        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var ability = player.Abilities.FirstOrDefault(x => x.Name == useAbilityAction.AbilityName);
        if (ability is null)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability {useAbilityAction.AbilityName} not found"
            );
        }

        var target = combatants.FirstOrDefault(x => x.CreatureId == useAbilityAction.TargetId);
        if (target is null)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Target {useAbilityAction.TargetId} not found"
            );
        }

        if (
            player.CooldownRemainingByAbility.TryGetValue(
                useAbilityAction.AbilityName,
                out var cooldown
            )
            && cooldown > 0
        )
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability '{useAbilityAction.AbilityName}' is on cooldown for {cooldown} more round(s)."
            );
        }

        switch (ability)
        {
            case SupportAbility when target != player:
                return PlayerCombatActionResolverResult.Failure(
                    $"Ability {useAbilityAction.AbilityName} can only be cast on {player.Name}"
                );
            case AttackAbility when target == player:
                return PlayerCombatActionResolverResult.Failure(
                    $"Ability {useAbilityAction.AbilityName} cannot target {player.Name}"
                );
        }

        if (!target.IsAlive)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Target {useAbilityAction.TargetId} is already dead"
            );
        }

        if (!AbilityGearRequirement.IsMet(player, ability))
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability {useAbilityAction.AbilityName} requires {AbilityGearRequirement.DescribeRequirement(ability)} to be equipped."
            );
        }

        if (player.CurrentAp < ability.ApCost)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability {useAbilityAction.AbilityName} costs {ability.ApCost} AP but {player.Name} only has {player.CurrentAp}"
            );
        }

        if (player.CurrentMp < ability.MpCost)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Ability {useAbilityAction.AbilityName} costs {ability.MpCost} MP but {player.Name} only has {player.CurrentMp}"
            );
        }

        var targets = ability switch
        {
            AttackAbility { TargetType: AttackTargetType.Aoe } => enemies
                .Where(e => e.IsAlive)
                .ToArray(),
            SupportAbility { TargetType: TargetType.Aoe } => [player],
            _ => [target],
        };

        return PlayerCombatActionResolverResult.Success(
            new ResolvedUseAbilityAction(ability, targets)
        );
    }

    private static PlayerCombatActionResolverResult ResolveUseItemAction(
        IReadOnlyList<Combatant> combatants,
        UseItemAction useItemAction
    )
    {
        var player = combatants.SingleOrDefault(c => c.IsPlayer);
        if (player is null)
        {
            return PlayerCombatActionResolverResult.Failure("No active player found in combat.");
        }

        var item = player.ConsumableItemSnapshots.FirstOrDefault(i =>
            i.Name == useItemAction.ItemName
        );
        if (item is null)
        {
            return PlayerCombatActionResolverResult.Failure(
                $"Item {useItemAction.ItemName} not found"
            );
        }

        return PlayerCombatActionResolverResult.Success(new ResolvedUseItemAction(item));
    }
}
