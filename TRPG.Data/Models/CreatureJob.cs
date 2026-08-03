namespace TRPG.Data.Models;

public enum CreatureJobAction
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

public class CreatureJob
{
    public CreatureJobAction Action { get; init; }
    public Guid CreatureId { get; init; }
    public Guid? DistrictId { get; init; }
    public int EndHour { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Priority { get; init; }
    public Guid? RoomId { get; init; }
    public DayOfWeek? SpecificDay { get; init; }
    public int StartHour { get; init; }
    public Guid StateId { get; init; }
    public Guid WorldId { get; init; }
}
