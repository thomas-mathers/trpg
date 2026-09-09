namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum TrapEncounterResolutionOutcome
{
    Disarmed,
    Survived,
    Fell,
    Withdrew,
}

[Tapper.TranspilationSource]
public record TrapEncounterResolutionFact(
    Guid EncounterId,
    TrapEncounterResolutionOutcome Outcome,
    TrapKind TrapKind,
    string? TargetLocationName
);
