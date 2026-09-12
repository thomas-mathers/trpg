namespace TRPG.Domain.Models;

public class Container : Prop, ILockableProp
{
    public Guid? KeyItemId { get; init; }
    public int? StorageSize { get; init; }
    public bool IsLocked { get; set; }
    public int LockLevel { get; init; }
}
