using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonPopulatorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly DungeonPopulator _dungeonPopulator = new(Builders.MakeCreatureGenerator());
    private readonly IReadOnlyDictionary<CreatureType, Faction> _factionsByCreatureType =
        EncounterFactionGenerator.Generate(Guid.NewGuid());

    private DungeonPopulatorInput MakeInput(BuildingType dungeonType)
    {
        return new DungeonPopulatorInput
        {
            LocationId = _locationId,
            WorldId = _worldId,
            DungeonType = dungeonType,
            FactionsByCreatureType = _factionsByCreatureType,
        };
    }

    [Fact]
    public void Generate_PlacesMonstersInTheRoom_WithLevelsInRange()
    {
        for (var i = 0; i < 20; i++)
        {
            // Act
            var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Cave));

            // Assert
            Assert.InRange(result.Monsters.Count, 1, 3);
            Assert.All(result.Monsters, m => Assert.Equal(_locationId, m.Creature.LocationId));
            Assert.All(result.Monsters, m => Assert.InRange(m.Creature.Level, 1, 3));
        }
    }

    [Fact]
    public void Generate_ThemesCaveMonsters_AsBeastsOrGoblins()
    {
        for (var i = 0; i < 20; i++)
        {
            // Act
            var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Cave));

            // Assert
            Assert.All(
                result.Monsters,
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
            var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Crypt));

            // Assert
            Assert.All(
                result.Monsters,
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
                dungeonType =>
                    Assert.NotEmpty(_dungeonPopulator.Generate(MakeInput(dungeonType)).Monsters)
            );
        }
    }

    [Fact]
    public void Generate_LeavesMonstersInertToTheLivingWorld()
    {
        // Act
        var monsters = _dungeonPopulator.Generate(MakeInput(BuildingType.Tower)).Monsters;

        // Assert — no profession, and a fixed description instead of a generated life story
        Assert.All(monsters, m => Assert.Null(m.Creature.Profession));
        Assert.All(monsters, m => Assert.False(string.IsNullOrWhiteSpace(m.Creature.Biography)));
    }

    [Fact]
    public void Generate_CreatesOneFactionAlignedGroup_ContainingEveryMonster()
    {
        // Act
        var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Cave));

        // Assert
        var group = Assert.Single(result.EncounterGroups);
        Assert.Equal(
            result.Monsters.Select(monster => monster.Creature.Id).OrderBy(id => id),
            result.EncounterGroupMembers.Select(member => member.CreatureId).OrderBy(id => id)
        );
        var creatureType = Assert.Single(
            result.Monsters.Select(monster => monster.Creature.CreatureType).Distinct()
        );
        Assert.Equal(_factionsByCreatureType[creatureType].Id, group.FactionId);
    }

    [Fact]
    public void Generate_GivesCaveMonsters_ADiurnalSleepSchedule()
    {
        // Act
        var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Cave));

        // Assert
        Assert.All(result.Monsters, monster => AssertDiurnalSchedule(result, monster));
    }

    [Fact]
    public void Generate_GivesCryptMonsters_ANocturnalSleepSchedule()
    {
        // Act
        var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Crypt));

        // Assert
        Assert.All(result.Monsters, monster => AssertNocturnalSchedule(result, monster));
    }

    [Fact]
    public void Generate_GivesConstructMonsters_NoSleepSchedule()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var result = _dungeonPopulator.Generate(MakeInput(BuildingType.Mine));

            // Assert
            var constructs = result.Monsters.Where(m =>
                m.Creature.CreatureType == CreatureType.Construct
            );
            Assert.All(
                constructs,
                monster =>
                    Assert.DoesNotContain(result.Jobs, job => job.CreatureId == monster.Creature.Id)
            );
        }
    }

    private static void AssertDiurnalSchedule(
        DungeonPopulatorResult result,
        CreatureGeneratorResult monster
    )
    {
        var jobs = result.Jobs.Where(job => job.CreatureId == monster.Creature.Id).ToArray();
        var sleep = Assert.Single(jobs, job => job.Action == CreatureJobAction.Sleep);
        var idle = Assert.Single(jobs, job => job.Action == CreatureJobAction.Idle);
        Assert.Equal(22, sleep.StartHour);
        Assert.Equal(6, sleep.EndHour);
        Assert.Equal(6, idle.StartHour);
        Assert.Equal(22, idle.EndHour);
    }

    private static void AssertNocturnalSchedule(
        DungeonPopulatorResult result,
        CreatureGeneratorResult monster
    )
    {
        var jobs = result.Jobs.Where(job => job.CreatureId == monster.Creature.Id).ToArray();
        var sleep = Assert.Single(jobs, job => job.Action == CreatureJobAction.Sleep);
        var idle = Assert.Single(jobs, job => job.Action == CreatureJobAction.Idle);
        Assert.Equal(6, sleep.StartHour);
        Assert.Equal(22, sleep.EndHour);
        Assert.Equal(22, idle.StartHour);
        Assert.Equal(6, idle.EndHour);
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
