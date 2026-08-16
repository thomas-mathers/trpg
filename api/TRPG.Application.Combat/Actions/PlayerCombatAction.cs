namespace TRPG.Application.Combat;

public abstract record PlayerCombatAction;

public sealed record UseAbilityAction(Guid TargetId, string AbilityName) : PlayerCombatAction;

public sealed record UseItemAction(string ItemName) : PlayerCombatAction;
