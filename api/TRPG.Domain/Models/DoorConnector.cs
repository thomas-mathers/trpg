namespace TRPG.Domain.Models;

public class DoorConnector
{
    public Guid ConnectorId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsLocked { get; set; }
    public int LockLevel { get; set; }
    public GameInstant? UnlocksAtGameTime { get; set; }
    public Guid WorldId { get; init; }
}
