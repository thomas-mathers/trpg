namespace TRPG.Data.Models;

public class BuildingOwner
{
    public Guid BuildingId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerId { get; init; }
    public Guid WorldId { get; init; }
}
