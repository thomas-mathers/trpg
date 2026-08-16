using System.Text.Json.Serialization;

namespace TRPG.Encounters.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AttackEncounterActionRequest), nameof(AttackEncounterActionRequest))]
[JsonDerivedType(typeof(EvadeEncounterActionRequest), nameof(EvadeEncounterActionRequest))]
[JsonDerivedType(typeof(RetreatEncounterActionRequest), nameof(RetreatEncounterActionRequest))]
public abstract record EncounterActionRequest;

public sealed record AttackEncounterActionRequest : EncounterActionRequest;

public sealed record EvadeEncounterActionRequest : EncounterActionRequest;

public sealed record RetreatEncounterActionRequest : EncounterActionRequest;
