using TRPG.Application.Abilities;

namespace TRPG.Application.Combat;

public abstract record PlayerRoundAction;

public sealed record UseAbility(string AbilityName, string TargetName) : PlayerRoundAction;

public sealed record UseItem(string ItemName) : PlayerRoundAction;

internal abstract record ResolvedAction;

internal sealed record ResolvedAbility(Ability Ability, IReadOnlyList<Combatant> Targets)
    : ResolvedAction;

internal sealed record ResolvedItem(UsableItem Item) : ResolvedAction;

internal abstract record ActionResolution;

internal sealed record ActionResolved(ResolvedAction Action) : ActionResolution;

internal sealed record ActionRejected(string Reason) : ActionResolution;

internal static class PlayerActionResolver
{
    public static ActionResolution Resolve(
        IReadOnlyList<Combatant> combatants,
        string actionName,
        string targetName
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        PlayerRoundAction playerAction = player.Abilities.Any(a => a.Name == actionName)
            ? new UseAbility(actionName, targetName)
            : new UseItem(actionName);

        return Resolve(combatants, playerAction);
    }

    public static ActionResolution Resolve(
        IReadOnlyList<Combatant> combatants,
        PlayerRoundAction playerAction
    )
    {
        var player = combatants.Single(c => c.IsPlayer);

        return playerAction switch
        {
            UseAbility useAbility => ResolveAbility(
                combatants,
                useAbility.AbilityName,
                useAbility.TargetName
            ),
            UseItem useItem => ResolveItem(player, useItem.ItemName),
            _ => new ActionRejected("Unrecognized action."),
        };
    }

    private static ActionResolution ResolveAbility(
        IReadOnlyList<Combatant> combatants,
        string abilityName,
        string targetName
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var ability = player.Abilities.FirstOrDefault(x => x.Name == abilityName);
        if (ability is null)
        {
            return new ActionRejected($"Ability {abilityName} not found");
        }

        var target = combatants.FirstOrDefault(x => x.Name == targetName);
        if (target is null)
        {
            return new ActionRejected($"Target {targetName} not found");
        }

        var cooldownRemaining = player.CooldownRemainingByAbility[abilityName];
        if (cooldownRemaining > 0)
        {
            return new ActionRejected(
                $"Ability {abilityName} is on cooldown for {cooldownRemaining} more rounds"
            );
        }

        switch (ability)
        {
            case SupportAbility when target != player:
                return new ActionRejected(
                    $"Ability {abilityName} can only be cast on {player.Name}"
                );
            case AttackAbility when target == player:
                return new ActionRejected($"Ability {abilityName} cannot target {player.Name}");
        }

        if (!target.IsAlive)
        {
            return new ActionRejected($"Target {targetName} is already dead");
        }

        if (player.CurrentAp < ability.ApCost)
        {
            return new ActionRejected(
                $"Ability {abilityName} costs {ability.ApCost} AP but {player.Name} only has {player.CurrentAp}"
            );
        }

        if (player.CurrentMp < ability.MpCost)
        {
            return new ActionRejected(
                $"Ability {abilityName} costs {ability.MpCost} MP but {player.Name} only has {player.CurrentMp}"
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

        return new ActionResolved(new ResolvedAbility(ability, targets));
    }

    private static ActionResolution ResolveItem(Combatant player, string itemName)
    {
        var item = player.UsableItems.FirstOrDefault(i => i.Name == itemName);
        if (item is null)
        {
            return new ActionRejected($"Item {itemName} not found");
        }

        return new ActionResolved(new ResolvedItem(item));
    }
}
