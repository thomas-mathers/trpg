using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

public enum TrapEncounterResolutionOutcome
{
    Disarmed,
    Survived,
    Fell,
    Withdrew,
}

public record TrapEncounterResolutionFact(
    Guid EncounterId,
    TrapEncounterResolutionOutcome Outcome,
    TrapKind TrapKind,
    string? TargetLocationName
);
