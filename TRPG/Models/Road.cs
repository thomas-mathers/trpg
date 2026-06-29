namespace TRPG.Models;

internal class Road {
    public float DangerLevel { get; init; }
    public Guid DestinationCityId { get; init; }
    public float Distance { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid OriginCityId { get; init; }
    public int TravelTime { get; init; }
}