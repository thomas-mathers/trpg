namespace TRPG.Models;

public class BuildingOwner
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BuildingId { get; init; }
    public Guid OwnerId { get; init; }
}
