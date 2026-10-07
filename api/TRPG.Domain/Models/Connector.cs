namespace TRPG.Domain.Models;

public abstract class Connector
{
    public double Distance { get; init; }
    public Guid DestinationNodeId { get; set; } = Guid.NewGuid();
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OriginNodeId { get; set; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
}
