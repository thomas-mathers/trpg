using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class DungeonPopulatorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly DungeonPopulator _dungeonPopulator = new(Builders.MakeCreatureGenerator());

    private DungeonPopulatorInput MakeInput(BuildingType dungeonType)
    {
        return new DungeonPopulatorInput
        {
            LocationId = _locationId,
            WorldId = _worldId,
            DungeonType = dungeonType,
        };
    }

    [Fact]
    public void Generate_PlacesMonstersInTheRoom_WithLevelsInRange()
    {
        for (var i = 0; i < 20; i++)
        {
            // Act
            var monsters = _dungeonPopulator.Generate(MakeInput(BuildingType.Cave));

            // Assert
            Assert.InRange(monsters.Count, 1, 3);
            Assert.All(monsters, m => Assert.Equal(_locationId, m.Creature.LocationId));
            Assert.All(monsters, m => Assert.InRange(m.Creature.Level, 1, 3));
        }
    }

    [Fact]
    public void Generate_ThemesCaveMonsters_AsBeastsOrGoblins()
    {
        for (var i = 0; i < 20; i++)
        {
            // Act
            var monsters = _dungeonPopulator.Generate(MakeInput(BuildingType.Cave));

            // Assert
            Assert.All(
                monsters,
                m =>
                    Assert.Contains(
                        m.Creature.CreatureType,
                        new[] { CreatureType.Beast, CreatureType.Goblin }
                    )
            );
        }
    }

    [Fact]
    public void Generate_ThemesCryptMonsters_AsUndeadOrWraiths()
    {
        for (var i = 0; i < 20; i++)
        {
            // Act
            var monsters = _dungeonPopulator.Generate(MakeInput(BuildingType.Crypt));

            // Assert
            Assert.All(
                monsters,
                m =>
                    Assert.Contains(
                        m.Creature.CreatureType,
                        new[] { CreatureType.Undead, CreatureType.Wraith }
                    )
            );
        }
    }

    [Fact]
    public void Generate_ProducesMonsters_ForEveryDungeonType()
    {
        // Assert — repeated generation across every themed dungeon type exercises each themed
        // archetype, so a registry row missing its CreatureType fails here instead of mid-worldgen
        for (var i = 0; i < 20; i++)
        {
            Assert.All(
                BuildingTypes.Dungeon,
                dungeonType => Assert.NotEmpty(_dungeonPopulator.Generate(MakeInput(dungeonType)))
            );
        }
    }

    [Fact]
    public void Generate_LeavesMonstersInertToTheLivingWorld()
    {
        // Act
        var monsters = _dungeonPopulator.Generate(MakeInput(BuildingType.Tower));

        // Assert — no profession, and a fixed description instead of a generated life story
        Assert.All(monsters, m => Assert.Null(m.Creature.Profession));
        Assert.All(monsters, m => Assert.False(string.IsNullOrWhiteSpace(m.Creature.Biography)));
    }

    [Fact]
    public void SupportsDungeonType_MatchesBuildingTypesDungeon_Exactly()
    {
        // Assert — keeps the scene's dungeon/building split from drifting out of sync
        // with the building types this populator actually knows how to populate
        Assert.All(
            BuildingTypes.Dungeon,
            t => Assert.True(DungeonPopulator.SupportsDungeonType(t))
        );
        Assert.All(
            Enum.GetValues<BuildingType>().Except(BuildingTypes.Dungeon),
            t => Assert.False(DungeonPopulator.SupportsDungeonType(t))
        );
    }
}
