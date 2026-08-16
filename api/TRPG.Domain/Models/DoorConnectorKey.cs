namespace TRPG.Domain.Models;

public class DoorConnectorKey
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public Guid DoorConnectorId { get; init; }
    public Guid WorldId { get; init; }
}
