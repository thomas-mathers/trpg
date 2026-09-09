namespace TRPG.Worlds.Requests;

public record PrefetchDungeonPremisesRequest
{
    public required IReadOnlyCollection<Guid> BuildingIds { get; init; }
}
