using TRPG.Application.Creatures.Results;
using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

public static class SceneResultBuilder
{
    public static SceneResult MakeScene(
        string? room = null,
        string? building = null,
        IReadOnlyCollection<string>? others = null,
        IReadOnlyCollection<SceneExitInfo>? exits = null
    ) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new SceneDateInfo(975, "Frostwane", 1, "Emberday", 8),
            new SceneStateInfo("Ravenhollow Territory", null),
            new SceneCityInfo("Ravenhollow", null),
            new SceneDistrictInfo(Guid.NewGuid(), "The Merchant Quarter", DistrictType.CityCenter),
            building == null
                ? null
                : new SceneBuildingInfo(building, BuildingType.Jail, null, null, null, null),
            room == null ? null : new SceneRoomInfo(room, "A cell.", 0),
            MakeCreature("Thomas Mathers"),
            exits ?? [],
            [],
            (others ?? []).Select(MakeCreature).ToArray(),
            [],
            null,
            [],
            new SceneLayoutInfo(new Footprint(10, 10), [], [], [], [])
        );

    public static SceneExitInfo MakeExit(string destinationName, bool isLocked) =>
        new(
            Guid.NewGuid(),
            $"A door to {destinationName}.",
            new SceneRoomExitDestination(destinationName, BuildingType.Jail, Role: null),
            isLocked,
            Direction: null,
            IsVisited: false,
            IsWayBack: false
        );

    public static SceneCreatureInfo MakeCreature(string name) =>
        new(
            Guid.NewGuid(),
            name,
            CreatureType.Human,
            Gender.Male,
            Profession.Knight,
            Level: 1,
            Age: 30,
            FactionNames: [],
            Condition: CreatureCondition.Awake,
            Activity: null,
            Posture: CreaturePosture.Standing,
            Movement: CreatureMovement.Stationary,
            IsSneaking: false,
            IsAlerted: false,
            IsRestrained: false,
            Reputation: null,
            Gold: 0,
            CurrentHp: 10,
            MaximumHp: 10,
            CurrentAp: 0,
            MaximumAp: 0,
            CurrentMp: 0,
            MaximumMp: 0,
            ExperienceCurrent: 0,
            ExperienceToNextLevel: 2,
            Strength: 5,
            Dexterity: 5,
            Intelligence: 5,
            Endurance: 5,
            Stamina: 5,
            Mana: 5,
            Defense: 5,
            MovementSpeed: 0,
            PhysicalResistance: 0,
            FireResistance: 0,
            IceResistance: 0,
            LightningResistance: 0,
            PoisonResistance: 0,
            MagicResistance: 0,
            TradeWorkstationId: null,
            QuestMarkers: [],
            ReadyToDeliver: false,
            Effects: CreatureEffects.None
        );
}
