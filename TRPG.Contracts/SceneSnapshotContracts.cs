namespace TRPG.Contracts;

public record SceneSnapshot(
    string RegionName,
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
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyBuildings,
    IReadOnlyCollection<NearbyExitSnapshot> Exits
);

public record NearbyPersonSnapshot(
    string Name,
    string CreatureType,
    string Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    string State,
    int Reputation
);

public record NearbyBuildingSnapshot(string Name, string Type);

public record NearbyExitSnapshot(string Description, string DestinationRoomName);
