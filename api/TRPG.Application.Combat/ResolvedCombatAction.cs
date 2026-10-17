using TRPG.Application.Abilities;

namespace TRPG.Application.Combat;

public abstract record ResolvedCombatAction;

internal sealed record ResolvedUseAbilityAction(Ability Ability, IReadOnlyList<Combatant> Targets)
    : ResolvedCombatAction;

internal sealed record ResolvedUseItemAction(ConsumableItemSnapshot Item) : ResolvedCombatAction;
