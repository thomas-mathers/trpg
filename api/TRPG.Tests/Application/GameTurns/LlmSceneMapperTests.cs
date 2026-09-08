using System.Text.Json;
using TRPG.Application.Common.Serialization;
using TRPG.Application.GameTurns.Mappers;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public sealed class LlmSceneMapperTests
{
    [Fact]
    public void ToLlmScene_KeepsWhatTheNarratorCanObserve()
    {
        // Arrange
        var scene = MakeScene();

        // Act
        var result = scene.ToLlmScene();

        // Assert
        var creature = Assert.Single(result.NearbyCreatures);
        Assert.Equal("Cora", creature.Name);
        Assert.Equal(Profession.Guard, creature.Profession);
        Assert.Equal(49, creature.Level);
        Assert.Equal(CreatureState.Busy, creature.State);
        Assert.Equal(-50, creature.Reputation);
        Assert.Equal(700, creature.CurrentHp);
        Assert.True(creature.CanTrade);
    }

    [Fact]
    public void ToLlmScene_OmitsMechanicalFieldsTheNarratorCannotUse()
    {
        // Arrange
        var scene = MakeScene();

        // Act
        var json = JsonSerializer.Serialize(scene.ToLlmScene(), TrpgJsonOptions.Default);

        // Assert — ids are unusable because every tool addresses things by name, gold is private,
        // and the stat block is what creature_inspect exists to fetch on demand.
        Assert.DoesNotContain("\"id\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("experienceCurrent", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("strength", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fireResistance", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("movementSpeed", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("maximumAp", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToLlmScene_KeepsTheGoldTheNarratorNarratesForThePlayerOnly()
    {
        // Arrange
        var scene = MakeScene();

        // Act
        var result = scene.ToLlmScene();

        // Assert
        Assert.Equal(45, result.Player.Gold);
        var json = JsonSerializer.Serialize(result.NearbyCreatures, TrpgJsonOptions.Default);
        Assert.DoesNotContain("gold", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToLlmScene_FlattensAnExitToItsDestinationName()
    {
        // Arrange
        var scene = MakeScene();

        // Act
        var result = scene.ToLlmScene();

        // Assert
        var exit = Assert.Single(result.Exits);
        Assert.Equal("The Forge Ward", exit.DestinationName);
        Assert.False(exit.IsLocked);
    }

    private static SceneResult MakeScene() =>
        new(
            Guid.NewGuid(),
            new SceneDateInfo(975, "Frostwane", 1, "Emberday", 8),
            new SceneStateInfo("Darkstead Territory", "The territory around Darkstead."),
            new SceneCityInfo("Darkstead", "The capital."),
            new SceneDistrictInfo(Guid.NewGuid(), "The Grand Bazaar", DistrictType.CityCenter),
            Building: null,
            Room: null,
            MakeCreature("Thomas", gold: 45, profession: Profession.Knight, level: 1),
            [
                new SceneExitInfo(
                    "A path leading to The Forge Ward.",
                    new SceneDistrictExitDestination("The Forge Ward", DistrictType.CityCenter),
                    IsLocked: false
                ),
            ],
            [new ScenePropInfo(Guid.NewGuid(), "Chair", "A wooden chair.", "Seat")],
            [MakeCreature("Cora", gold: 1449, profession: Profession.Guard, level: 49)],
            [new SceneNearbyBuildingInfo(Guid.NewGuid(), "The Trading Post", BuildingType.Inn)]
        );

    private static SceneCreatureInfo MakeCreature(
        string name,
        int gold,
        Profession profession,
        int level
    ) =>
        new(
            Guid.NewGuid(),
            name,
            CreatureType.Human,
            Gender.Female,
            profession,
            level,
            Age: 68,
            FactionNames: [],
            State: CreatureState.Busy,
            IsSneaking: false,
            Reputation: -50,
            Gold: gold,
            CurrentHp: 700,
            MaximumHp: 756,
            CurrentAp: 146,
            MaximumAp: 146,
            CurrentMp: 2,
            MaximumMp: 2,
            ExperienceCurrent: 45,
            ExperienceToNextLevel: 98,
            Strength: 95,
            Dexterity: 47,
            Intelligence: 1,
            Endurance: 122,
            Stamina: 36,
            Mana: 1,
            Defense: 109,
            MovementSpeed: 0,
            PhysicalResistance: 0,
            FireResistance: 0.02f,
            IceResistance: 0,
            LightningResistance: 0,
            PoisonResistance: 0,
            MagicResistance: 0,
            TradeWorkstationId: Guid.NewGuid(),
            QuestMarker: null
        );
}
