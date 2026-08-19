using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public enum GuardEncounterResolutionOutcome
{
    PaidFine,
    WentToJail,
    ResistedArrest,
}

[TranspilationSource]
public record GuardEncounterResolutionFact(
    Guid EncounterId,
    GuardEncounterResolutionOutcome Outcome,
    string GuardName,
    string LocationName,
    int? FineAmount,
    int? JailHours
);
