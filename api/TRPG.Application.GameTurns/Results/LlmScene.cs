using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Results;

// What the narrator can observe. Ids are omitted because everything is addressed by name, and
// the mechanical stat block because creature_inspect fetches it on demand for whoever needs it.
public record LlmSceneDate(int Year, string MonthName, int Day, string WeekdayName, int Hour);

public record LlmSceneRegion(string Name, string? Description);

public record LlmSceneDistrict(string Name, DistrictType Type);

public record LlmSceneBuilding(
    string Name,
    BuildingType Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);

public record LlmSceneRoom(string Name, string Description, int FloorNumber);

public record LlmSceneExit(
    string Description,
    string DestinationName,
    bool IsLocked,
    CompassDirection? Direction
);

public record LlmSceneProp(string Name, string Description, string Type);

public record LlmSceneNearbyBuilding(string Name, BuildingType Type);

public record LlmScenePlayer(
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    bool IsSneaking,
    int Gold,
    int CurrentHp,
    int MaximumHp
);

public record LlmSceneCreature(
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    CreatureState? State,
    bool IsSneaking,
    int? Reputation,
    int CurrentHp,
    int MaximumHp,
    bool CanTrade,
    QuestMarker? QuestMarker
);

public record LlmScene(
    LlmSceneDate CurrentDate,
    LlmSceneRegion? Region,
    LlmSceneRegion? City,
    LlmSceneDistrict? District,
    LlmSceneBuilding? Building,
    LlmSceneRoom? Room,
    LlmScenePlayer Player,
    IReadOnlyCollection<LlmSceneExit> Exits,
    IReadOnlyCollection<LlmSceneProp> NearbyProps,
    IReadOnlyCollection<LlmSceneCreature> NearbyCreatures,
    IReadOnlyCollection<LlmSceneNearbyBuilding> NearbyBuildings
);
