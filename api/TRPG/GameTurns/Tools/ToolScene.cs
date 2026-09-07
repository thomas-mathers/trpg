using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.GameTurns.Tools;

// What the narrator can observe. Ids are omitted because every tool addresses things by name, and
// the mechanical stat block because creature_inspect fetches it on demand for whoever needs it.
internal record ToolSceneDate(int Year, string MonthName, int Day, string WeekdayName, int Hour);

internal record ToolSceneRegion(string Name, string? Description);

internal record ToolSceneDistrict(string Name, DistrictType Type);

internal record ToolSceneBuilding(
    string Name,
    BuildingType Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);

internal record ToolSceneRoom(string Name, string Description, int FloorNumber);

internal record ToolSceneExit(string Description, string DestinationName, bool IsLocked);

internal record ToolSceneProp(string Name, string Description, string Type);

internal record ToolSceneNearbyBuilding(string Name, BuildingType Type);

internal record ToolScenePlayer(
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

internal record ToolSceneCreature(
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

internal record ToolScene(
    ToolSceneDate CurrentDate,
    ToolSceneRegion? Region,
    ToolSceneRegion? City,
    ToolSceneDistrict? District,
    ToolSceneBuilding? Building,
    ToolSceneRoom? Room,
    ToolScenePlayer Player,
    IReadOnlyCollection<ToolSceneExit> Exits,
    IReadOnlyCollection<ToolSceneProp> NearbyProps,
    IReadOnlyCollection<ToolSceneCreature> NearbyCreatures,
    IReadOnlyCollection<ToolSceneNearbyBuilding> NearbyBuildings
);
