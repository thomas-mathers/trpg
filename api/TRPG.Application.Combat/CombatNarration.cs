using TRPG.Application.Combat.Events;

namespace TRPG.Application.Combat;

public static class CombatNarration
{
    public static IReadOnlyList<string> Describe(IReadOnlyList<CombatResolution> events) =>
        events.Select(Describe).OfType<string>().ToArray();

    public static string? Describe(CombatResolution combatEvent) =>
        combatEvent switch
        {
            Hit { IsCritical: true } hit =>
                $"{hit.AttackerName} critically hits {hit.TargetName} with {hit.AbilityName} for {hit.Damage} damage.",
            Hit hit when hit.Killed =>
                $"{hit.AttackerName} defeats {hit.TargetName} with {hit.AbilityName}.",
            Hit hit =>
                $"{hit.AttackerName} hits {hit.TargetName} with {hit.AbilityName} for {hit.Damage} damage.",
            Miss miss => $"{miss.AttackerName}'s {miss.AbilityName} misses {miss.TargetName}.",
            Block block => $"{block.TargetName} blocks {block.AttackerName}'s {block.AbilityName}.",
            Healed healed =>
                $"{healed.SourceName} restores {healed.Amount} HP to {healed.TargetName}.",
            ConsumedPotion potion => $"{potion.CreatureName} uses {potion.ItemName}.",
            _ => null,
        };
}
