namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum GuardEncounterResolutionOutcome
{
    PaidFine,
    WentToJail,
    ResistedArrest,
}

[Tapper.TranspilationSource]
public record GuardEncounterResolutionFact(
    Guid EncounterId,
    GuardEncounterResolutionOutcome Outcome,
    string GuardName,
    string LocationName,
    int? FineAmount,
    int? JailHours
);
