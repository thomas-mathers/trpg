namespace TRPG.Domain.Models;

public class Container : Prop
{
    public Guid? KeyItemId { get; init; }
    public Guid? OwnerCreatureId { get; set; }
    public int? StorageSize { get; init; }
}
