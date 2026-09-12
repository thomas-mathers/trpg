using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonInhabitantGeneratorTests
{
    private readonly DungeonInhabitantGenerator _generator = new(Builders.MakeCreatureGenerator());

    private static DungeonGeneratorResult MakeDungeon(int seed)
    {
        var wilderness = Builders.MakeLocation(Guid.NewGuid(), Guid.NewGuid());
        return DungeonGenerator.Generate(
            new DungeonGeneratorInput([], wilderness, wilderness.WorldId)
            {
                Random = new Random(seed),
            }
        );
    }

    [Fact]
    public void Generate_ReturnsBothOutcomes_AcrossManySeeds()
    {
        var results = Enumerable
            .Range(1, 200)
            .Select(seed =>
                _generator.Generate(new DungeonInhabitantInput(MakeDungeon(seed), new Random(seed)))
            )
            .ToArray();

        // Assert — with a 40% roll and a matching room role in every dungeon type, both
        // outcomes must show up; if either vanished the roll or the room lookup broke
        Assert.Contains(results, result => result != null);
        Assert.Contains(results, result => result == null);
    }

    [Fact]
    public void Generate_PlacesTheInhabitant_InARoomMatchingItsDungeonTypesCatalogRole()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            // Act
            var dungeon = MakeDungeon(seed);
            var result = _generator.Generate(new DungeonInhabitantInput(dungeon, new Random(seed)));

            // Assert
            if (result == null)
            {
                continue;
            }

            var spec = DungeonInhabitantCatalog.SpecFor(dungeon.Building.BuildingType)!;
            var placement = Assert.Single(
                dungeon.Placements,
                room => room.Room.LocationId == result.LocationId
            );
            Assert.Equal(spec.Role, placement.Role);
            Assert.Equal(result.LocationId, result.Participant.Creature.LocationId);
        }
    }

    [Fact]
    public void Generate_GivesTheInhabitant_StationarySleepAndIdleJobs()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            // Act
            var result = _generator.Generate(
                new DungeonInhabitantInput(MakeDungeon(seed), new Random(seed))
            );

            // Assert
            if (result == null)
            {
                continue;
            }

            Assert.All(result.Jobs, job => Assert.Equal(result.LocationId, job.LocationId));
            Assert.All(
                result.Jobs,
                job => Assert.Equal(result.Participant.Creature.Id, job.CreatureId)
            );
            Assert.Contains(result.Jobs, job => job.Action == CreatureJobAction.Sleep);
            Assert.Contains(result.Jobs, job => job.Action == CreatureJobAction.Idle);
        }
    }

    [Fact]
    public void Generate_UsesAFriendlyGoblin_ForACaveScavenger()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            // Act
            var dungeon = MakeDungeon(seed);
            if (dungeon.Building.BuildingType != BuildingType.Cave)
            {
                continue;
            }
            var result = _generator.Generate(new DungeonInhabitantInput(dungeon, new Random(seed)));

            // Assert
            if (result == null)
            {
                continue;
            }

            Assert.Equal(CreatureType.Goblin, result.Participant.Creature.CreatureType);
            Assert.Null(result.Participant.Creature.SpawnerId);
        }
    }

    [Fact]
    public void Generate_GivesTheInhabitant_ABespokeConversationProfile()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            // Act
            var dungeon = MakeDungeon(seed);
            var result = _generator.Generate(new DungeonInhabitantInput(dungeon, new Random(seed)));

            // Assert
            if (result == null)
            {
                continue;
            }

            Assert.Equal(result.Participant.Creature.Id, result.Profile.CreatureId);
            Assert.False(string.IsNullOrWhiteSpace(result.Profile.PrivateBackground.Profession));
        }
    }
}
