namespace TRPG.Domain.Models;

public interface ILockableProp
{
    bool IsLocked { get; set; }
    int LockLevel { get; }
    Guid? KeyItemId { get; }
}

// A locked prop a creature can be held inside, rather than a location behind a door — a captive
// can still be talked to before they're freed, in the same room the player is already in.
public class Cell : Prop, ILockableProp
{
    public Guid? CreatureId { get; set; }
    public bool IsLocked { get; set; }
    public int LockLevel { get; init; }
    public Guid? KeyItemId { get; init; }
}
