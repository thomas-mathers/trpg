namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum SuspicionEncounterResolutionOutcome
{
    Complied,
    Fled,
    FleeFailed,
}

[Tapper.TranspilationSource]
public record SuspicionEncounterResolutionFact(
    Guid EncounterId,
    SuspicionEncounterResolutionOutcome Outcome,
    string GuardName,
    string LocationName,
    Guid? EscalatedGuardEncounterId
);
