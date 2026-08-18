namespace TRPG.Domain.Models;

public enum CreatureJobAction
{
    Sleep,
    Work,
    Idle,
    Study,
    Pray,
}

public class CreatureJob
{
    public CreatureJobAction Action { get; init; }
    public Guid CreatureId { get; init; }
    public int EndHour { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public int Priority { get; init; }
    public DayOfWeek? SpecificDay { get; init; }
    public int StartHour { get; init; }
    public Guid WorldId { get; init; }
}
