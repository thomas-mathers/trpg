namespace TRPG.Application.Encounters.Results;

public record EncounterActionResult(
    HostileEncounterActionKind ActionKind,
    EncounterResolutionFact Fact
);
