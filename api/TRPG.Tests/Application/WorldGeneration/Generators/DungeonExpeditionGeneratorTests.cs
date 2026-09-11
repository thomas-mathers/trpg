using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonExpeditionGeneratorTests
{
    private readonly DungeonExpeditionGenerator _generator = new(Builders.MakeCreatureGenerator());

    [Theory]
    [InlineData(7)]
    [InlineData(31)]
    [InlineData(103)]
    public void Generate_CreatesConnectedPersistentParticipants_WhenADungeonIsEligible(int seed)
    {
        // Arrange
        var wilderness = Builders.MakeLocation(Guid.NewGuid(), Guid.NewGuid());
        var random = new Random(seed);
        var dungeon = DungeonGenerator.Generate(
            new DungeonGeneratorInput([], wilderness, wilderness.WorldId) { Random = random }
        );

        // Act
        var result = _generator.Generate(new DungeonExpeditionInput([dungeon], [], [], random));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dungeon.EntranceLocationId, result.Participants[0].Creature.LocationId);
        Assert.Equal(CreatureState.Dead, result.Participants[1].Creature.State);
        Assert.Equal(0, result.Participants[1].Creature.CurrentHp);
        Assert.All(result.Participants, participant => Assert.Null(participant.Creature.SpawnerId));
        Assert.All(result.Jobs, job => Assert.Equal(result.Expedition.SurvivorId, job.CreatureId));
        Assert.Equal(result.Expedition.CompanionId, result.Journal.Ownership.OwnerId);
        Assert.Equal(result.Work.Id, result.Journal.WorkId);
        Assert.Equal(result.Secret.Id, result.Work.SecretId);
        Assert.Equal(2, result.Profiles.Count);
        var placement = Assert.Single(
            dungeon.Placements,
            room => room.Room.LocationId == result.Expedition.CompanionLocationId
        );
        Assert.True(placement.DepthFromEntrance >= 2);
        Assert.NotEqual(RoomRole.BossChamber, placement.Role);
        Assert.Contains(placement.Room.Name, result.Expedition.Discovery, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ReturnsNothing_WhenNoDungeonsExist()
    {
        // Act
        var result = _generator.Generate(new DungeonExpeditionInput([], [], [], new Random(3)));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Generate_ReturnsNothing_WhenEveryDeeperRoomHasASpawner()
    {
        // Arrange
        var wilderness = Builders.MakeLocation(Guid.NewGuid(), Guid.NewGuid());
        var dungeon = DungeonGenerator.Generate(
            new DungeonGeneratorInput([], wilderness, wilderness.WorldId) { Random = new Random(8) }
        );
        var spawners = dungeon
            .Placements.Select(room =>
                Builders.MakeCreatureSpawner(wilderness.WorldId, room.Room.LocationId)
            )
            .ToArray();

        // Act
        var result = _generator.Generate(
            new DungeonExpeditionInput([dungeon], spawners, [], new Random(8))
        );

        // Assert
        Assert.Null(result);
    }
}
