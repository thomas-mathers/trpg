namespace TRPG.Models;

internal class Prop {
    public Rectangle Boundary { get; init; } = null!;
    public Guid RoomId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public List<ItemCategory>? StorageItemCategories { get; init; }
    public int? StorageSize { get; init; }
}
