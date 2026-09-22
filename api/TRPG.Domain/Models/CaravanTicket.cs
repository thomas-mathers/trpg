namespace TRPG.Domain.Models;

public class CaravanTicket
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RouteTravelerId { get; init; }
    public required Guid CreatureId { get; init; }
    public required Guid OriginStopLocationId { get; init; }
    public required Guid DestinationLocationId { get; init; }
    public required TimeSpan PurchasedAtPlaytime { get; init; }
}
