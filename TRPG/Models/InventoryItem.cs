namespace TRPG.Models;

internal class InventoryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid ItemId { get; init; }
    public int Quantity { get; set; }
    public int Index { get; init; }
    public EquipmentSlot? EquippedSlot { get; set; }
    public Item Item { get; init; } = null!;
}