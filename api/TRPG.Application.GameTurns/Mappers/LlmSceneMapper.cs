using TRPG.Application.GameTurns.Results;

namespace TRPG.Application.GameTurns.Mappers;

public static class LlmSceneMapper
{
    public static LlmScene ToLlmScene(this SceneResult scene) =>
        new(
            scene.CurrentDate.ToLlmSceneDate(),
            scene.State == null
                ? null
                : new LlmSceneRegion(scene.State.Name, scene.State.Description),
            scene.City == null ? null : new LlmSceneRegion(scene.City.Name, scene.City.Description),
            scene.District == null
                ? null
                : new LlmSceneDistrict(scene.District.Name, scene.District.Type),
            scene.Building?.ToLlmSceneBuilding(),
            scene.Room?.ToLlmSceneRoom(),
            scene.Player.ToLlmScenePlayer(),
            scene.Exits.Select(exit => exit.ToLlmSceneExit()).ToArray(),
            scene.NearbyProps.Select(prop => prop.ToLlmSceneProp()).ToArray(),
            scene.NearbyCreatures.Select(creature => creature.ToLlmSceneCreature()).ToArray(),
            scene.NearbyBuildings.Select(building => building.ToLlmSceneNearbyBuilding()).ToArray()
        );

    private static LlmSceneDate ToLlmSceneDate(this SceneDateInfo date) =>
        new(date.Year, date.MonthName, date.Day, date.WeekdayName, date.Hour);

    private static LlmSceneBuilding ToLlmSceneBuilding(this SceneBuildingInfo building) =>
        new(
            building.Name,
            building.Type,
            building.OwnerName,
            building.FactionName,
            building.FactionDescription
        );

    private static LlmSceneRoom ToLlmSceneRoom(this SceneRoomInfo room) =>
        new(room.Name, room.Description, room.FloorNumber);

    private static LlmSceneExit ToLlmSceneExit(this SceneExitInfo exit) =>
        new(exit.Description, exit.Destination.Name, exit.IsLocked);

    private static LlmSceneProp ToLlmSceneProp(this ScenePropInfo prop) =>
        new(prop.Name, prop.Description, prop.Type);

    private static LlmSceneNearbyBuilding ToLlmSceneNearbyBuilding(
        this SceneNearbyBuildingInfo building
    ) => new(building.Name, building.Type);

    private static LlmScenePlayer ToLlmScenePlayer(this SceneCreatureInfo creature) =>
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

    private static LlmSceneCreature ToLlmSceneCreature(this SceneCreatureInfo creature) =>
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
