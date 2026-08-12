namespace TRPG.Data.Models;

public class TravelConnector
{
    public Guid ConnectorId { get; init; }
    public float DangerLevel { get; init; }
    public float Distance { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int TravelTimeHours { get; init; }
    public Guid WorldId { get; init; }
}
