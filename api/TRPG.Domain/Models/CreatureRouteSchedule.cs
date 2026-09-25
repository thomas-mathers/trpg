namespace TRPG.Domain.Models;

public class CreatureRouteSchedule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid CreatureId { get; init; }
    public Guid RouteId { get; init; }
    public Guid OriginCreatureJobId { get; init; }
    public Guid DestinationCreatureJobId { get; init; }
    public DayOfWeek DepartureDay { get; init; }
    public required double DepartureHour { get; init; }
    public required double DurationHours { get; init; }
    public required string Purpose { get; init; }
}
