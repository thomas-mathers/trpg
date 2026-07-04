namespace TRPG.Models;

internal class Road {
    public float DangerLevel { get; init; }
    public Guid DestinationStateId { get; init; }
    public float Distance { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid OriginStateId { get; init; }
    public int TravelTime { get; init; }
    public Guid WorldId { get; init; }
}