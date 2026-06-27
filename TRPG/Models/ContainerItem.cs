namespace TRPG.Models;

internal class ContainerItem {
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Index { get; init; }
    public Guid ItemId { get; init; }
    public Guid ContainerId { get; init; }
    public int Quantity { get; init; }
}
