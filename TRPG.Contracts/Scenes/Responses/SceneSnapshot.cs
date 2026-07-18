namespace TRPG.Contracts.Scenes.Responses;

public record SceneSnapshot(
    string StateName,
    string? CityName,
    string? DistrictName,
    string? BuildingName,
    string? RoomName,
    int Year,
    string MonthName,
    int Day,
    string WeekdayName,
    int Hour,
    CreatureStatusSnapshot PlayerStatus,
    IReadOnlyCollection<CreatureStatusSnapshot> NearbyCreatures,
    IReadOnlyCollection<NearbyDistrictSnapshot> NearbyDistricts,
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyBuildings,
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyDungeons,
    IReadOnlyCollection<NearbyPropSnapshot> NearbyProps,
    IReadOnlyCollection<NearbyExitSnapshot> Exits
);

public record CreatureStatusSnapshot(
    string Name,
    string CreatureType,
    string Gender,
    string Profession,
    int Level,
    int Age,
    string State,
    int Gold,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    IReadOnlyCollection<string>? FactionNames,
    int? Reputation
);

public record NearbyDistrictSnapshot(string Name, string Type);

public record NearbyBuildingSnapshot(string Name, string Type);

public record NearbyPropSnapshot(string Name, string Type);

public record NearbyExitSnapshot(string Description, string DestinationRoomName);
