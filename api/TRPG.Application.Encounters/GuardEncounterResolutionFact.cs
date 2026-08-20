namespace TRPG.Application.Encounters;

public enum GuardEncounterResolutionOutcome
{
    PaidFine,
    WentToJail,
    ResistedArrest,
}

public record GuardEncounterResolutionFact(
    Guid EncounterId,
    GuardEncounterResolutionOutcome Outcome,
    string GuardName,
    string LocationName,
    int? FineAmount,
    int? JailHours
);
