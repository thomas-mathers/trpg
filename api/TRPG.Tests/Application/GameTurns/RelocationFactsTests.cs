using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public sealed class RelocationFactsTests
{
    [Fact]
    public void Describe_SaysThePlayerIsAlone_WhenNobodyElseIsThere()
    {
        // Arrange — a cell the narrator was never shown is a cell it furnishes with a guard.
        var scene = MakeScene(room: "Cells", building: "The Iron Gate", others: []);

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("now in Cells in The Iron Gate", text);
        Assert.Contains("alone here", text);
    }

    [Fact]
    public void Describe_NamesEveryoneElsePresent()
    {
        // Arrange
        var scene = MakeScene(
            room: "Shop",
            building: "The Fine Filigree",
            others: ["Rowena Oakheart", "Calder Kingsley"]
        );

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("Rowena Oakheart, Calder Kingsley", text);
        Assert.DoesNotContain("alone", text);
    }

    [Fact]
    public void Describe_FallsBackToTheDistrict_WhenTheArrivalIsOutdoors()
    {
        // Arrange
        var scene = MakeScene(room: null, building: null, others: []);

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("now in The Merchant Quarter.", text);
    }

    private static SceneResult MakeScene(
        string? room,
        string? building,
        IReadOnlyCollection<string> others
    ) =>
        new(
            Guid.NewGuid(),
            new SceneDateInfo(975, "Frostwane", 1, "Emberday", 8),
            new SceneStateInfo("Ravenhollow Territory", null),
            new SceneCityInfo("Ravenhollow", null),
            new SceneDistrictInfo(Guid.NewGuid(), "The Merchant Quarter", DistrictType.CityCenter),
            building == null
                ? null
                : new SceneBuildingInfo(building, BuildingType.Jail, null, null, null),
            room == null ? null : new SceneRoomInfo(room, "A cell.", 0),
            MakeCreature("Thomas Mathers"),
            [],
            [],
            others.Select(MakeCreature).ToArray(),
            []
        );

    private static SceneCreatureInfo MakeCreature(string name) =>
        new(
            Guid.NewGuid(),
            name,
            CreatureType.Human,
            Gender.Male,
            Profession.Knight,
            Level: 1,
            Age: 30,
            FactionNames: [],
            State: null,
            IsSneaking: false,
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
            QuestMarker: null
        );
}
