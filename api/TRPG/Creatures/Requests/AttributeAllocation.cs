using TRPG.Data.Models;

namespace TRPG.Creatures.Requests;

public record AttributeAllocation
{
    public int Strength { get; init; }
    public int Dexterity { get; init; }
    public int Endurance { get; init; }
    public int Stamina { get; init; }
    public int Mana { get; init; }
    public int Intelligence { get; init; }
}
