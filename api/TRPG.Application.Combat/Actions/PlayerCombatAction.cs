using System.Text.Json.Serialization;

namespace TRPG.Application.Combat;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UseAbilityAction), nameof(UseAbilityAction))]
[JsonDerivedType(typeof(UseItemAction), nameof(UseItemAction))]
public abstract record PlayerCombatAction;

public sealed record UseAbilityAction(Guid TargetId, string AbilityName) : PlayerCombatAction;

public sealed record UseItemAction(string ItemName) : PlayerCombatAction;
