namespace TRPG.Models;

internal enum JobAction
{
    Sleep,
    Work,
    Idle,
    Patrol,
    Socialize,
    Study,
    Pray,
    Train,
    Sit,
}

internal class Job
{
    public JobAction Action { get; init; }
    public Guid CreatureId { get; init; }
    public int EndHour { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Priority { get; init; }
    public Guid? RoomId { get; init; }
    public DayOfWeek? SpecificDay { get; init; }
    public int StartHour { get; init; }
    public Guid StateId { get; init; }
    public Guid WorldId { get; init; }
}
