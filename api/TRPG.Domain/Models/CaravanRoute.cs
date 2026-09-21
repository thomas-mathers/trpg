namespace TRPG.Domain.Models;

public class CaravanRoute
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public required string Name { get; init; }
    public required int TicketFeeGold { get; init; }
    public required double LingerHours { get; init; }
}
