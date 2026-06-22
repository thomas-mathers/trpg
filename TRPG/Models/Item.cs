namespace TRPG.Models;

internal class Item {
    public List<Guid> ActiveEffectIds { get; init; } = [];
    public ItemCategory Category { get; init; }
    public string Description { get; init; } = "";
    public int GoldValue { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsStackable { get; init; }
    public string Name { get; init; } = "";
    public List<Guid> PassiveEffectIds { get; init; } = [];
    public int Weight { get; init; }
    public Guid WorldId { get; init; }
}