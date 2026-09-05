namespace TRPG.Application.Encounters;

public enum SuspicionEncounterResolutionOutcome
{
    Complied,
    Fled,
    FleeFailed,
}

public record SuspicionEncounterResolutionFact(
    Guid EncounterId,
    SuspicionEncounterResolutionOutcome Outcome,
    string GuardName,
    string LocationName,
    Guid? EscalatedGuardEncounterId
);
