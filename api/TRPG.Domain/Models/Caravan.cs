namespace TRPG.Domain.Models;

public enum CaravanDirection
{
    Clockwise,
    CounterClockwise,
}

public class Caravan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid CaravanRouteId { get; init; }
    public required double PhaseOffsetHours { get; init; }
    public required CaravanDirection Direction { get; init; }
}
