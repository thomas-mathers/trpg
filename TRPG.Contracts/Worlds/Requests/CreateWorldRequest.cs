using TRPG.Contracts.Creatures.Requests;

namespace TRPG.Contracts.Worlds.Requests;

public enum Gender
{
    Male,
    Female,
}

public enum Race
{
    Human,
    Elf,
    Dwarf,
    Orc,
    Halfling,
    Gnome,
}

public enum PlayerClass
{
    Knight,
    Rogue,
    Ranger,
    Mage,
    Cleric,
}

public record CreateWorldRequest
{
    public required string PlayerName { get; init; }
    public required Gender Gender { get; init; }
    public required int Age { get; init; }
    public required Race Race { get; init; }
    public required PlayerClass PlayerClass { get; init; }
    public IReadOnlyDictionary<
        AllocatableAttributeName,
        int
    > StartingAttributeAllocation { get; init; } = new Dictionary<AllocatableAttributeName, int>();
    public string Description { get; init; } = "";
    public int MinCityStates { get; init; }
    public int MaxCityStates { get; init; }
    public int MinRuralStates { get; init; }
    public int MaxRuralStates { get; init; }
    public int MinBuildingsPerState { get; init; }
    public int MaxBuildingsPerState { get; init; }
    public int MinFactionMembers { get; init; }
    public int MaxFactionMembers { get; init; }
    public int HousesPerCity { get; init; }
    public int MinHouseholdSize { get; init; }
    public int MaxHouseholdSize { get; init; }
    public int FactionCount { get; init; }
}
