namespace TRPG.Models;

internal class Item
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<Guid> ActiveEffectIds { get; init; } = [];
    public List<Guid> PassiveEffectIds { get; init; } = [];
    public ItemCategory Category { get; init; }
    public bool IsStackable { get; init; }
    public int Weight { get; init; }
    public int GoldValue { get; init; }
}
