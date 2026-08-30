namespace TRPG.Domain.Models;

public class RestockPolicy
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid WorkstationId { get; init; }
    public int TriggerHour { get; init; }
    public DayOfWeek? SpecificDay { get; init; }
    public TimeSpan LastSyncPlaytime { get; set; }
}
