namespace TRPG.Domain.Models;

public class RouteStop
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RouteId { get; init; }
    public required int SequenceIndex { get; init; }
    public required Guid LocationId { get; init; }
    public required float DistanceToNextStop { get; init; }
}
