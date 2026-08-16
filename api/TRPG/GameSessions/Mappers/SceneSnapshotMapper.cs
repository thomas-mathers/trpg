using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneSnapshotMapper
{
    public static SceneSnapshot ToSnapshot(this SceneResult scene) =>
        new(
            WorldId: scene.WorldId,
            StateName: scene.State?.Name ?? "",
            CityName: scene.City?.Name,
            DistrictName: scene.District?.Name,
            BuildingName: scene.Building?.Name,
            RoomName: scene.Room?.Name,
            Year: scene.CurrentDate.Year,
            MonthName: scene.CurrentDate.MonthName,
            Day: scene.CurrentDate.Day,
            WeekdayName: scene.CurrentDate.WeekdayName,
            Hour: scene.CurrentDate.Hour,
            PlayerStatus: scene.Player.ToStatusSnapshot(),
            NearbyCreatures: scene
                .NearbyCreatures.Select(creature => creature.ToStatusSnapshot())
                .ToArray(),
            NearbyBuildings: scene
                .NearbyBuildings.Select(building => building.ToSnapshot())
                .ToArray(),
            NearbyProps: scene.NearbyProps.Select(prop => prop.ToSnapshot()).ToArray(),
            Exits: scene.Exits.Select(exit => exit.ToSnapshot()).ToArray()
        );
}
