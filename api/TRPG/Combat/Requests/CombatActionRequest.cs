using System.Text.Json.Serialization;

namespace TRPG.Combat.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UseAbilityCombatActionRequest), nameof(UseAbilityCombatActionRequest))]
[JsonDerivedType(typeof(UseItemCombatActionRequest), nameof(UseItemCombatActionRequest))]
public abstract record CombatActionRequest;

public sealed record UseAbilityCombatActionRequest(Guid TargetId, string AbilityName)
    : CombatActionRequest;

public sealed record UseItemCombatActionRequest(string ItemName) : CombatActionRequest;
