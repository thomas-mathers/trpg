using TRPG.Application.GameTurns.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneSnapshotMapper
{
    public static SceneSnapshot ToSnapshot(this SceneResult scene, WorldStateStamp stamp) =>
        new(
            WorldId: scene.WorldId,
            StateName: scene.State?.Name ?? "",
            CityName: scene.City?.Name,
            DistrictName: scene.District?.Name,
            BuildingName: scene.Building?.Name,
            RoomName: scene.Room?.Name,
            PlayerStatus: scene.Player.ToStatusSnapshot(),
            NearbyCreatures: scene
                .NearbyCreatures.Select(creature => creature.ToStatusSnapshot())
                .ToArray(),
            NearbyBuildings: scene
                .NearbyBuildings.Select(building => building.ToSnapshot())
                .ToArray(),
            NearbyProps: scene.NearbyProps.Select(prop => prop.ToSnapshot()).ToArray(),
            Exits: scene.Exits.Select(exit => exit.ToSnapshot()).ToArray(),
            NearbyCaravans: scene.NearbyCaravans.Select(caravan => caravan.ToSnapshot()).ToArray(),
            Version: stamp.Version,
            GameTimeMilliseconds: (long)(stamp.GameTime - GameClock.Epoch).TotalMilliseconds,
            AnchoredAtUnixMilliseconds: stamp.CapturedAt.ToUnixTimeMilliseconds()
        );
}
