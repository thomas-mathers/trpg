namespace TRPG.Models;

internal enum JobAction {
    Sleep,
    Work,
    Idle,
    Patrol,
    Socialize
}

internal class Job {
    public JobAction Action { get; init; }
    public bool Daily { get; init; }
    public int EndHour { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RegionId { get; init; }
    public Guid? RoomId { get; init; }
    public Guid PersonId { get; init; }
    public int Priority { get; init; }
    public int StartHour { get; init; }
}