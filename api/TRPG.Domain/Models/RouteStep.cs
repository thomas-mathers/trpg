namespace TRPG.Domain.Models;

public class RouteStep
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RouteId { get; init; }
    public required int SequenceIndex { get; init; }
    public required Guid LocationId { get; init; }
    public Guid? ConnectorId { get; init; }
    public required double DwellHours { get; init; }
}
