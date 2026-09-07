using TRPG.Application.GameTurns.Results;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class ToolSceneMapper
{
    public static ToolScene ToToolScene(this SceneResult scene) =>
        new(
            scene.CurrentDate.ToToolSceneDate(),
            scene.State == null
                ? null
                : new ToolSceneRegion(scene.State.Name, scene.State.Description),
            scene.City == null
                ? null
                : new ToolSceneRegion(scene.City.Name, scene.City.Description),
            scene.District == null
                ? null
                : new ToolSceneDistrict(scene.District.Name, scene.District.Type),
            scene.Building?.ToToolSceneBuilding(),
            scene.Room?.ToToolSceneRoom(),
            scene.Player.ToToolScenePlayer(),
            scene.Exits.Select(exit => exit.ToToolSceneExit()).ToArray(),
            scene.NearbyProps.Select(prop => prop.ToToolSceneProp()).ToArray(),
            scene.NearbyCreatures.Select(creature => creature.ToToolSceneCreature()).ToArray(),
            scene.NearbyBuildings.Select(building => building.ToToolSceneNearbyBuilding()).ToArray()
        );

    private static ToolSceneDate ToToolSceneDate(this SceneDateInfo date) =>
        new(date.Year, date.MonthName, date.Day, date.WeekdayName, date.Hour);

    private static ToolSceneBuilding ToToolSceneBuilding(this SceneBuildingInfo building) =>
        new(
            building.Name,
            building.Type,
            building.OwnerName,
            building.FactionName,
            building.FactionDescription
        );

    private static ToolSceneRoom ToToolSceneRoom(this SceneRoomInfo room) =>
        new(room.Name, room.Description, room.FloorNumber);

    private static ToolSceneExit ToToolSceneExit(this SceneExitInfo exit) =>
        new(exit.Description, exit.Destination.Name, exit.IsLocked);

    private static ToolSceneProp ToToolSceneProp(this ScenePropInfo prop) =>
        new(prop.Name, prop.Description, prop.Type);

    private static ToolSceneNearbyBuilding ToToolSceneNearbyBuilding(
        this SceneNearbyBuildingInfo building
    ) => new(building.Name, building.Type);

    private static ToolScenePlayer ToToolScenePlayer(this SceneCreatureInfo creature) =>
        new(
            creature.Name,
            creature.CreatureType,
            creature.Gender,
            creature.Profession,
            creature.Level,
            creature.Age,
            creature.FactionNames,
            creature.IsSneaking,
            creature.Gold,
            creature.CurrentHp,
            creature.MaximumHp
        );

    private static ToolSceneCreature ToToolSceneCreature(this SceneCreatureInfo creature) =>
        new(
            creature.Name,
            creature.CreatureType,
            creature.Gender,
            creature.Profession,
            creature.Level,
            creature.Age,
            creature.FactionNames,
            creature.State,
            creature.IsSneaking,
            creature.Reputation,
            creature.CurrentHp,
            creature.MaximumHp,
            CanTrade: creature.TradeWorkstationId != null,
            creature.QuestMarker
        );
}
