namespace TRPG.Domain.Models;

public class RouteTraveler
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RouteId { get; init; }
    public required GameInstant StartedAtGameTime { get; set; }
    public GameInstant? PausedAtGameTime { get; set; }
    public required double SpeedUnitsPerHour { get; init; }
    public string? Purpose { get; init; }
    public Guid? CreatureRouteScheduleId { get; init; }
}
