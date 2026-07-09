using System.Collections.ObjectModel;

namespace TRPG.Data.Models;

public class Container : Prop
{
    public Collection<ContainerItem> Items { get; init; } = [];
    public Guid? KeyItemId { get; init; }
    public int? StorageSize { get; init; }
}
