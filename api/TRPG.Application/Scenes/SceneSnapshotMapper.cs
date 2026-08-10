using TRPG.Application.Common.Mappers;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Application.Scenes;

public static class SceneSnapshotMapper
{
    public static SceneSnapshot ToSnapshot(SceneResult scene)
    {
        return new SceneSnapshot(
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
            PlayerStatus: ToCreatureStatusSnapshot(scene.Player),
            NearbyCreatures: scene.NearbyCreatures.Select(ToCreatureStatusSnapshot).ToArray(),
            NearbyBuildings: scene
                .NearbyBuildings.Select(b =>
                {
                    var type = b.Type.ToContract();
                    return new NearbyBuildingSnapshot(b.Id, b.Name, type, type.ToDisplayName());
                })
                .ToArray(),
            NearbyProps: scene
                .NearbyProps.Select(p => new NearbyPropSnapshot(p.Id, p.Name, p.Type))
                .ToArray(),
            Exits: scene.Exits.Select(ToNearbyExitSnapshot).ToArray()
        );
    }

    private static NearbyExitSnapshot ToNearbyExitSnapshot(SceneExitInfo exit) =>
        new(
            exit.Description,
            exit.Destination switch
            {
                SceneDistrictExitDestination district => new DistrictExitDestination(
                    district.Name,
                    district.DistrictType.ToContract()
                ),
                SceneBuildingExitDestination building => new BuildingExitDestination(
                    building.Name,
                    building.BuildingType.ToContract()
                ),
                SceneRoomExitDestination room => new RoomExitDestination(
                    room.Name,
                    room.BuildingType.ToContract()
                ),
                SceneWildernessExitDestination wilderness => new WildernessExitDestination(
                    wilderness.Name
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(exit)),
            }
        );

    private static CreatureStatusSnapshot ToCreatureStatusSnapshot(SceneCreatureInfo creature) =>
        new(
            Id: creature.Id,
            Name: creature.Name,
            CreatureType: creature.CreatureType.ToContract(),
            Gender: creature.Gender.ToContract(),
            Profession: creature.Profession?.ToContract(),
            Level: creature.Level,
            Age: creature.Age,
            State: creature.State?.ToContract(),
            Gold: creature.Gold,
            CurrentHp: creature.CurrentHp,
            MaximumHp: creature.MaximumHp,
            CurrentAp: creature.CurrentAp,
            MaximumAp: creature.MaximumAp,
            CurrentMp: creature.CurrentMp,
            MaximumMp: creature.MaximumMp,
            ExperienceCurrent: creature.ExperienceCurrent,
            ExperienceToNextLevel: creature.ExperienceToNextLevel,
            FactionNames: creature.FactionNames,
            Reputation: creature.Reputation,
            Strength: creature.Strength,
            Dexterity: creature.Dexterity,
            Intelligence: creature.Intelligence,
            Endurance: creature.Endurance,
            Stamina: creature.Stamina,
            Mana: creature.Mana,
            Defense: creature.Defense,
            MovementSpeed: creature.MovementSpeed,
            PhysicalResistance: creature.PhysicalResistance,
            FireResistance: creature.FireResistance,
            IceResistance: creature.IceResistance,
            LightningResistance: creature.LightningResistance,
            PoisonResistance: creature.PoisonResistance,
            MagicResistance: creature.MagicResistance,
            TradeWorkstationId: creature.TradeWorkstationId
        );
}
