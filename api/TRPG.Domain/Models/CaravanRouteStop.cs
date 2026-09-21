namespace TRPG.Domain.Models;

public class CaravanRouteStop
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CaravanRouteId { get; init; }
    public required int SequenceIndex { get; init; }
    public required Guid LocationId { get; init; }
    public required float DistanceToNextStop { get; init; }
}
