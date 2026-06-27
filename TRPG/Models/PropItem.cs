namespace TRPG.Models;

internal class PropItem {
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Index { get; init; }
    public Guid ItemId { get; init; }
    public Guid PropId { get; init; }
    public int Quantity { get; init; }
}
