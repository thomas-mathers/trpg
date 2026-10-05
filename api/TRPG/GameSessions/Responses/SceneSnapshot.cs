using System.ComponentModel;
using System.Text.Json.Serialization;
using Tapper;
using TRPG.Application.Scenes.Events;
using TypedSignalR.Client;
using ActiveBuff = TRPG.Combat.Responses.ActiveBuff;
using ActiveConditions = TRPG.Combat.Responses.ActiveConditions;
using ActiveDot = TRPG.Combat.Responses.ActiveDot;
using ActiveHot = TRPG.Combat.Responses.ActiveHot;

namespace TRPG.GameSessions.Responses;

[TranspilationSource]
public enum CreatureType
{
    Human,
    Elf,
    Dwarf,
    Orc,
    Halfling,
    Gnome,
    Undead,
    Demon,
    Beast,
    Construct,
    Elemental,
    Goblin,
    Wraith,
    Giant,
    Dragon,
}

[TranspilationSource]
public enum Gender
{
    Male,
    Female,
}

[TranspilationSource]
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

    [Description("Stable Master")]
    StableMaster,
    Bartender,
    Guard,
    Baker,
    Innkeeper,
    Tailor,
    Carpenter,
    Jeweler,
    Homemaker,
    Unemployed,
}

[TranspilationSource]
public enum CreatureCondition
{
    Awake,
    Sleeping,
    Dead,
}

[TranspilationSource]
public enum CreatureActivity
{
    Working,
    Studying,
    Praying,
    Eating,
}

[TranspilationSource]
public enum CreatureMovement
{
    Stationary,
    Walking,
}

[TranspilationSource]
public enum CreaturePosture
{
    Standing,
    Sitting,
    Lying,
}

[TranspilationSource]
public enum RoomRole
{
    Entrance,

    [Description("Boss Chamber")]
    BossChamber,
    Passage,

    [Description("Guard Post")]
    GuardPost,
    Storeroom,

    [Description("Treasure Room")]
    TreasureRoom,
    Shrine,
    Study,

    [Description("Cell Block")]
    CellBlock,

    [Description("Collapsed Gallery")]
    CollapsedGallery,

    [Description("Flooded Sump")]
    FloodedSump,
}

[TranspilationSource]
public enum CompassDirection
{
    North,
    Northeast,
    East,
    Southeast,
    South,
    Southwest,
    West,
    Northwest,
}

[TranspilationSource]
public enum StairDirection
{
    Up,
    Down,
}

[TranspilationSource]
public enum DistrictType
{
    Residential,
    Scientific,

    [Description("City Center")]
    CityCenter,

    [Description("City Entrance")]
    CityEntrance,
    Governmental,

    [Description("Holy Site")]
    HolySite,
    Encampment,
}

[TranspilationSource]
public enum BuildingType
{
    [Description("Arcane Shop")]
    ArcaneShop,
    Apothecary,
    Bakery,
    Barracks,
    Blacksmith,
    Carpenter,
    Castle,
    Cave,
    Crypt,

    [Description("General Goods")]
    GeneralGoods,

    [Description("Guild Hall")]
    GuildHall,
    House,
    Inn,
    Jail,
    Jeweler,
    Library,
    Mine,
    Ruins,
    Stable,
    Tailor,
    Tavern,
    Temple,
    Tower,
}

[TranspilationSource]
public enum WeatherCondition
{
    Clear,
    Cloudy,
    Rain,
    Storm,
    Snow,
    Fog,
}

[TranspilationSource]
public record SceneSnapshot(
    Guid WorldId,
    Guid LocationId,
    string StateName,
    string? CityName,
    string? DistrictName,
    string? BuildingName,
    string? RoomName,
    WeatherCondition? Weather,
    CreatureStatusSnapshot PlayerStatus,
    IReadOnlyCollection<CreatureStatusSnapshot> NearbyCreatures,
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyBuildings,
    IReadOnlyCollection<NearbyPropSnapshot> NearbyProps,
    IReadOnlyCollection<NearbyExitSnapshot> Exits,
    IReadOnlyCollection<NearbyCaravanSnapshot> NearbyCaravans,
    FootprintWire Size,
    long Version,
    long GameTimeMilliseconds,
    long AnchoredAtUnixMilliseconds,
    double TimeScale,
    IReadOnlyCollection<GreenSpaceSnapshot>? GreenSpaces = null,
    LocationBoundarySnapshot? Boundary = null,
    IReadOnlyCollection<RoadSnapshot>? Roads = null,
    IReadOnlyCollection<NeighborSnapshot>? Neighbors = null
);

[TranspilationSource]
public record RoadSnapshot(
    IReadOnlyCollection<PointWire> Points,
    double Width,
    RoadClassSnapshot Class
);

[TranspilationSource]
public enum RoadClassSnapshot
{
    Avenue,
    Street,
    Lane,
}

[TranspilationSource]
public record NeighborSnapshot(
    Guid LocationId,
    IReadOnlyCollection<NearbyBuildingSnapshot> Buildings,
    IReadOnlyCollection<NearbyPropSnapshot> Props,
    IReadOnlyCollection<GreenSpaceSnapshot> GreenSpaces,
    IReadOnlyCollection<BoundarySegmentSnapshot> Segments,
    IReadOnlyCollection<RoadSnapshot> Roads
);

[TranspilationSource]
public record GreenSpaceSnapshot(Guid Id, PlacementWire Placement, FootprintWire Footprint);

[TranspilationSource]
public enum BoundarySegmentKind
{
    Wall,
    Tower,
}

[TranspilationSource]
public record BoundarySegmentSnapshot(
    BoundarySegmentKind Kind,
    PlacementWire Placement,
    FootprintWire Footprint
);

[TranspilationSource]
public record BoundaryGateSnapshot(Guid ConnectorId, PlacementWire Placement, double Width);

[TranspilationSource]
public record LocationBoundarySnapshot(
    IReadOnlyCollection<BoundarySegmentSnapshot> Segments,
    IReadOnlyCollection<BoundaryGateSnapshot> Gates,
    IReadOnlyCollection<CompassDirection> OpenEdges
);

[TranspilationSource]
public record CreatureStatusSnapshot(
    Guid Id,
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int Age,
    CreatureCondition Condition,
    CreatureActivity? Activity,
    CreaturePosture Posture,
    CreatureMovement Movement,
    bool IsSneaking,
    bool IsAlerted,
    bool IsRestrained,
    int Gold,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    int ExperienceCurrent,
    int ExperienceToNextLevel,
    IReadOnlyCollection<string>? FactionNames,
    int? Reputation,
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
    ActiveConditions ActiveConditions,
    IReadOnlyCollection<ActiveDot> ActiveDots,
    IReadOnlyCollection<ActiveHot> ActiveHots,
    IReadOnlyCollection<ActiveBuff> ActiveBuffs,
    PlacementWire Placement
);

[TranspilationSource]
public record QuestMarkerEntry(Guid QuestId, string Name, QuestMarker Marker);

[TranspilationSource]
public enum QuestMarker
{
    Available,
    ReadyToTurnIn,
}

[TranspilationSource]
public record NearbyBuildingSnapshot(
    Guid Id,
    string Name,
    BuildingType Type,
    string TypeDescription,
    PlacementWire Placement,
    FootprintWire Footprint,
    int FloorCount
);

[TranspilationSource]
public record NearbyPropSnapshot(
    Guid Id,
    string Name,
    string Description,
    string Type,
    bool IsOccupied,
    bool IsOccupiedByPlayer,
    PropModel Model,
    PlacementWire Placement,
    FootprintWire Footprint
);

[TranspilationSource]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DistrictExitDestination), "District")]
[JsonDerivedType(typeof(BuildingExitDestination), "Building")]
[JsonDerivedType(typeof(RoomExitDestination), "Room")]
[JsonDerivedType(typeof(WildernessExitDestination), "Wilderness")]
public abstract record NearbyExitDestination(string Name);

[TranspilationSource]
public sealed record DistrictExitDestination(string Name, DistrictType DistrictType)
    : NearbyExitDestination(Name);

[TranspilationSource]
public sealed record BuildingExitDestination(string Name, BuildingType BuildingType)
    : NearbyExitDestination(Name);

[TranspilationSource]
public sealed record RoomExitDestination(string Name, BuildingType BuildingType, RoomRole? Role)
    : NearbyExitDestination(Name);

[TranspilationSource]
public sealed record WildernessExitDestination(string Name) : NearbyExitDestination(Name);

[TranspilationSource]
public record NearbyExitSnapshot(
    Guid ConnectorId,
    string Description,
    NearbyExitDestination Destination,
    CompassDirection? Direction,
    bool IsVisited,
    bool IsWayBack,
    Guid DestinationLocationId,
    PlacementWire Placement,
    StairDirection? Stairs
);

[TranspilationSource]
public record CaravanDestinationSnapshot(
    Guid LocationId,
    string LocationName,
    int TravelTimeHours,
    bool HasTicket
);

[TranspilationSource]
public record NearbyCaravanSnapshot(
    Guid CaravanId,
    string RouteName,
    int TicketFeeGold,
    int MinutesUntilDeparture,
    bool PassengerServiceAvailable,
    IReadOnlyCollection<CaravanDestinationSnapshot> Destinations
);
