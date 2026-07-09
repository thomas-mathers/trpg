namespace TRPG.Models;

internal class InventoryItem
{
    public Guid CreatureId { get; init; }
    public EquipmentSlot? EquippedSlot { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Index { get; init; }
    public Item Item { get; init; } = null!;
    public Guid ItemId { get; init; }
    public int Quantity { get; set; }
    public Guid WorldId { get; init; }
}
