using TRPG.Application.Worlds;
using TRPG.Creatures.Requests;

namespace TRPG.Worlds.Requests;

public record CreateWorldRequest
{
    public required string PlayerName { get; init; }
    public required PlayerGender Gender { get; init; }
    public required int Age { get; init; }
    public required Race Race { get; init; }
    public required PlayerClass PlayerClass { get; init; }
    public AttributeAllocation StartingAttributeAllocation { get; init; } = new();
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
