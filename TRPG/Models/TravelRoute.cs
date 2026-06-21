namespace TRPG.Models;

internal class TravelRoute
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OriginCityId { get; init; }
    public Guid DestinationCityId { get; init; }
    public string Name { get; init; } = "";
    public float Distance { get; init; }
    public int TravelTime { get; init; }
    public float DangerLevel { get; init; }
}
