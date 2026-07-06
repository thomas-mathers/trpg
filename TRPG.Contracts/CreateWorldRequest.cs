namespace TRPG.Contracts;

public enum Race {
    Human,
    Elf,
    Dwarf,
    Orc,
    Halfling,
    Gnome,
}

public enum Profession {
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
    Guard
}

public record CreateWorldRequest {
    public required string PlayerName { get; init; }
    public required Race Race { get; init; }
    public required Profession Profession { get; init; }
    public string Description { get; init; } = WorldGenerationDefaults.Description;
    public int MinCityStates { get; init; } = WorldGenerationDefaults.MinCityStates;
    public int MaxCityStates { get; init; } = WorldGenerationDefaults.MaxCityStates;
    public int MinRuralStates { get; init; } = WorldGenerationDefaults.MinRuralStates;
    public int MaxRuralStates { get; init; } = WorldGenerationDefaults.MaxRuralStates;
    public int MinBuildingsPerState { get; init; } = WorldGenerationDefaults.MinBuildingsPerState;
    public int MaxBuildingsPerState { get; init; } = WorldGenerationDefaults.MaxBuildingsPerState;
    public int MinFactionMembers { get; init; } = WorldGenerationDefaults.MinFactionMembers;
    public int MaxFactionMembers { get; init; } = WorldGenerationDefaults.MaxFactionMembers;
    public int HousesPerCity { get; init; } = WorldGenerationDefaults.HousesPerCity;
    public int MinHouseholdSize { get; init; } = WorldGenerationDefaults.MinHouseholdSize;
    public int MaxHouseholdSize { get; init; } = WorldGenerationDefaults.MaxHouseholdSize;
    public int FactionCount { get; init; } = WorldGenerationDefaults.FactionCount;
}