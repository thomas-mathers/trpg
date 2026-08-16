using System.Text.Json.Serialization;

namespace TRPG.Application.Encounters;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AttackEncounterAction), nameof(AttackEncounterAction))]
[JsonDerivedType(typeof(EvadeEncounterAction), nameof(EvadeEncounterAction))]
[JsonDerivedType(typeof(RetreatEncounterAction), nameof(RetreatEncounterAction))]
public abstract record PlayerEncounterAction;

public sealed record AttackEncounterAction : PlayerEncounterAction;

public sealed record EvadeEncounterAction : PlayerEncounterAction;

public sealed record RetreatEncounterAction : PlayerEncounterAction;
