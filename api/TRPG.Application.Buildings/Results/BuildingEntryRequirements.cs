namespace TRPG.Application.Buildings.Results;

public enum BuildingEntryResult
{
    Entered,
    NoEntrance,
    Locked,
}

public record BuildingEntryRequirements(
    BuildingEntryResult Outcome,
    Guid? EntranceLocationId,
    IReadOnlyCollection<Guid>? ValidKeyItemIds = null
);
