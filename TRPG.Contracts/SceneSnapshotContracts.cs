namespace TRPG.Contracts;

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
    IReadOnlyCollection<NearbyPersonSnapshot> NearbyPeople,
    IReadOnlyCollection<NearbyDistrictSnapshot> NearbyDistricts,
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyBuildings,
    IReadOnlyCollection<NearbyPropSnapshot> NearbyProps,
    IReadOnlyCollection<NearbyExitSnapshot> Exits
);

public record NearbyPersonSnapshot(
    string Name,
    string CreatureType,
    string Gender,
    string Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    string State,
    int Reputation
);

public record NearbyDistrictSnapshot(string Name, string Type);

public record NearbyBuildingSnapshot(string Name, string Type);

public record NearbyPropSnapshot(string Name, string Type);

public record NearbyExitSnapshot(string Description, string DestinationRoomName);
