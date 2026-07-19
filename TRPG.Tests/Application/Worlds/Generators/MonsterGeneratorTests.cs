using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class MonsterGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();

    private MonsterGeneratorInput MakeInput(BuildingType dungeonType)
    {
        return new MonsterGeneratorInput
        {
            StateId = _stateId,
            RoomId = _roomId,
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
            var monsters = MonsterGenerator.Generate(MakeInput(BuildingType.Cave));

            // Assert
            Assert.InRange(monsters.Count, 1, 3);
            Assert.All(monsters, m => Assert.Equal(_roomId, m.Creature.RoomId));
            Assert.All(monsters, m => Assert.InRange(m.Creature.Level, 3, 12));
        }
    }

    [Fact]
    public void Generate_ThemesMonsterTypes_ByDungeonType()
    {
        for (var i = 0; i < 20; i++)
        {
            // Act
            var caveMonsters = MonsterGenerator.Generate(MakeInput(BuildingType.Cave));
            var cryptMonsters = MonsterGenerator.Generate(MakeInput(BuildingType.Crypt));

            // Assert
            Assert.All(
                caveMonsters,
                m => Assert.Equal(CreatureType.Beast, m.Creature.CreatureType)
            );
            Assert.All(
                cryptMonsters,
                m => Assert.Equal(CreatureType.Undead, m.Creature.CreatureType)
            );
        }
    }

    [Fact]
    public void Generate_LeavesMonstersInertToTheLivingWorld()
    {
        // Act
        var monsters = MonsterGenerator.Generate(MakeInput(BuildingType.Tower));

        // Assert — no profession, no city, and a fixed description instead of a generated life story
        Assert.All(monsters, m => Assert.Null(m.Creature.Profession));
        Assert.All(monsters, m => Assert.Null(m.Creature.CityId));
        Assert.All(monsters, m => Assert.False(string.IsNullOrWhiteSpace(m.Creature.Biography)));
    }

    [Fact]
    public void Generate_StartsMonstersAtFullResources()
    {
        // Act
        var monsters = MonsterGenerator.Generate(MakeInput(BuildingType.Cave));

        // Assert
        Assert.All(monsters, m => Assert.Equal(m.Creature.MaximumHp, m.Creature.CurrentHp));
        Assert.All(monsters, m => Assert.Equal(m.Creature.MaximumAp, m.Creature.CurrentAp));
        Assert.All(monsters, m => Assert.Equal(m.Creature.MaximumMp, m.Creature.CurrentMp));
        Assert.All(monsters, m => Assert.Equal(TimeSpan.Zero, m.Creature.LastRegenPlaytime));
    }

    [Fact]
    public void SupportsDungeonType_MatchesBuildingTypesDungeon_Exactly()
    {
        // Assert — keeps the scene's dungeon/building split from drifting out of sync
        // with the building types this generator actually knows how to populate
        Assert.All(
            BuildingTypes.Dungeon,
            t => Assert.True(MonsterGenerator.SupportsDungeonType(t))
        );
        Assert.All(
            Enum.GetValues<BuildingType>().Except(BuildingTypes.Dungeon),
            t => Assert.False(MonsterGenerator.SupportsDungeonType(t))
        );
    }
}
