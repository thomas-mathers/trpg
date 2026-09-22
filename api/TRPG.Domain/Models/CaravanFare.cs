namespace TRPG.Domain.Models;

public class CaravanFare
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RouteId { get; init; }
    public required int TicketFeeGold { get; init; }
}
