namespace TRPG.Models;

public class Job
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public JobAction Action { get; init; }
    public int StartHour { get; init; }
    public int EndHour { get; init; }
    public bool Daily { get; init; }
    public int Priority { get; init; }
    public Location Location { get; init; } = null!;
}
