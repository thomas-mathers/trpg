namespace TRPG.Contracts;

public record SceneStateInfo(string Name, string? Description);

public record SceneDistrictInfo(string Name, string Type, bool IsCurrent);

public record SceneCityInfo(string Name, string? Description, IReadOnlyCollection<SceneDistrictInfo> Districts);

public record SceneBuildingInfo(
    string Name,
    string Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription);

public record SceneExitInfo(string Description, string DestinationRoomName, bool IsLocked);

public record SceneRoomInfo(
    string Name,
    string Description,
    int FloorNumber,
    IReadOnlyCollection<SceneExitInfo> Exits);

public record ScenePlayerInfo(string Name, string Race, string Profession, int Level, int Gold, int Age);

public record ScenePropInfo(string Name, string Description, string Type);

public record ScenePersonInfo(
    string Name,
    string Race,
    string Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    string State,
    int Reputation);

public record SceneNearbyBuildingInfo(string Name, string Type);

public record SceneResult(
    InGameDate CurrentDate,
    SceneStateInfo? State,
    SceneCityInfo? City,
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    ScenePlayerInfo Player,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<ScenePersonInfo> NearbyPeople,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);
