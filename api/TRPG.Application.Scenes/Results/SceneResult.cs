using TRPG.Application.Creatures.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Results;

public record SceneDateInfo(int Year, string MonthName, int Day, string WeekdayName, int Hour);

public record SceneStateInfo(string Name, string? Description);

public record SceneDistrictInfo(Guid Id, string Name, DistrictType Type);

public record SceneCityInfo(string Name, string? Description);

public record SceneBuildingInfo(
    string Name,
    BuildingType Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription,
    string? Premise
);

public abstract record SceneExitDestination(string Name);

public sealed record SceneDistrictExitDestination(string Name, DistrictType DistrictType)
    : SceneExitDestination(Name);

public sealed record SceneBuildingExitDestination(string Name, BuildingType BuildingType)
    : SceneExitDestination(Name);

public sealed record SceneRoomExitDestination(
    string Name,
    BuildingType BuildingType,
    RoomRole? Role
) : SceneExitDestination(Name);

public sealed record SceneWildernessExitDestination(string Name) : SceneExitDestination(Name);

public record SceneExitInfo(
    Guid ConnectorId,
    string Description,
    SceneExitDestination Destination,
    bool IsLocked,
    CompassDirection? Direction,
    bool IsVisited,
    bool IsWayBack,
    Guid DestinationLocationId,
    Placement Placement
);

public record SceneRoomInfo(string Name, string Description, int FloorNumber);

public record ScenePropInfo(
    Guid Id,
    string Name,
    string Description,
    string Type,
    bool IsOccupied,
    bool IsOccupiedByPlayer,
    PropModel Model,
    Placement Placement,
    Footprint Footprint
);

public record SceneJourneyInfo(string Purpose, string NextDestination);

public record SceneCreatureInfo(
    Guid Id,
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    CreatureCondition Condition,
    CreatureActivity? Activity,
    CreaturePosture Posture,
    CreatureMovement Movement,
    bool IsSneaking,
    bool IsAlerted,
    bool IsRestrained,
    int? Reputation,
    int Gold,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    int ExperienceCurrent,
    int ExperienceToNextLevel,
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Mana,
    int Defense,
    float MovementSpeed,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance,
    Guid? TradeWorkstationId,
    IReadOnlyCollection<QuestMarkerEntry> QuestMarkers,
    bool ReadyToDeliver,
    CreatureEffects Effects,
    SceneJourneyInfo? Journey,
    Placement Placement
);

public record SceneNearbyBuildingInfo(
    Guid Id,
    string Name,
    BuildingType Type,
    Placement Placement,
    Footprint Footprint
);

public record SceneCaravanDestination(
    Guid LocationId,
    string LocationName,
    int TravelTimeHours,
    bool HasTicket
);

public record SceneCaravanInfo(
    Guid CaravanId,
    string RouteName,
    int TicketFeeGold,
    int MinutesUntilDeparture,
    bool PassengerServiceAvailable,
    IReadOnlyCollection<SceneCaravanDestination> Destinations
);

public record SceneResult(
    Guid WorldId,
    Guid LocationId,
    SceneDateInfo CurrentDate,
    SceneStateInfo? State,
    SceneCityInfo? City,
    SceneDistrictInfo? District,
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    SceneCreatureInfo Player,
    IReadOnlyCollection<SceneExitInfo> Exits,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneCreatureInfo> NearbyCreatures,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings,
    WeatherCondition? Weather,
    IReadOnlyCollection<SceneCaravanInfo> NearbyCaravans,
    Footprint Size
);
