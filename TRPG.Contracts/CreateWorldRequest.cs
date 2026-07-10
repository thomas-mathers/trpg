namespace TRPG.Contracts;

public enum Race
{
    Human,
    Elf,
    Dwarf,
    Orc,
    Halfling,
    Gnome,
}

public enum Profession
{
    Knight,
    Rogue,
    Ranger,
    Mage,
    Cleric,
    Mercenary,
    Alchemist,
    Blacksmith,
    Scholar,
    Merchant,
    Politician,
    StableMaster,
    Bartender,
    Guard,
}

public record CreateWorldRequest
{
    public required string PlayerName { get; init; }
    public required Race Race { get; init; }
    public required Profession Profession { get; init; }
    public string Description { get; init; }
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
