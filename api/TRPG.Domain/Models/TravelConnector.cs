namespace TRPG.Domain.Models;

public class TravelConnector
{
    public Guid ConnectorId { get; init; }
    public float DangerLevel { get; init; }
    public float Distance { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
}
