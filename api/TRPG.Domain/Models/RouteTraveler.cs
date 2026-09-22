namespace TRPG.Domain.Models;

public enum RouteDirection
{
    Clockwise,
    CounterClockwise,
}

public class RouteTraveler
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RouteId { get; init; }
    public required double PhaseOffsetHours { get; init; }
    public required RouteDirection Direction { get; init; }
}
