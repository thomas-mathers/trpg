namespace TRPG.Application.Encounters.Results;

public record HostileEncounterActionResult(
    HostileEncounterActionKind ActionKind,
    HostileEncounterResolutionFact Fact
);
