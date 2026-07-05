namespace TRPG.Contracts;

public record CreateWorldRequest {
    public required string PlayerName { get; init; }
    public required Profession Profession { get; init; }
    public string Description { get; init; } = WorldGenerationDefaults.Description;
    public int MinCountries { get; init; } = WorldGenerationDefaults.MinCountries;
    public int MaxCountries { get; init; } = WorldGenerationDefaults.MaxCountries;
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
    public int RaceCount { get; init; } = WorldGenerationDefaults.RaceCount;
    public int FactionCount { get; init; } = WorldGenerationDefaults.FactionCount;
}

public record CreateWorldResponse(Guid WorldId, Guid PlayerId, string WorldName);

public record WorldSummary(Guid WorldId, string Name, bool HasPlayer);
