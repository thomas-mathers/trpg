using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CreatureSpawnFillerTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _spawnerId = Guid.NewGuid();
    private readonly CreatureGenerator _creatureGenerator = Builders.MakeCreatureGenerator();
    private readonly IReadOnlyDictionary<CreatureType, Faction> _factionsByCreatureType =
        EncounterFactionGenerator.Generate(Guid.NewGuid());
    private readonly IReadOnlyList<CreatureType> _archetypeCreatureTypes =
    [
        CreatureType.Beast,
        CreatureType.Goblin,
    ];

    [Fact]
    public void Fill_ReturnsNothing_WhenPopulationAlreadyAtMax()
    {
        // Act
        var result = CreatureSpawnFiller.Fill(
            _creatureGenerator,
            _archetypeCreatureTypes,
            currentPopulation: 3,
            maxPopulation: 3,
            playerLevel: 1,
            _worldId,
            _locationId,
            _spawnerId,
            _factionsByCreatureType
        );

        // Assert
        Assert.Empty(result.Monsters);
        Assert.Empty(result.EncounterGroups);
    }

    [Fact]
    public void Fill_ReturnsNothing_WhenNoArchetypesConfigured()
    {
        // Act
        var result = CreatureSpawnFiller.Fill(
            _creatureGenerator,
            archetypeCreatureTypes: [],
            currentPopulation: 0,
            maxPopulation: 3,
            playerLevel: 1,
            _worldId,
            _locationId,
            _spawnerId,
            _factionsByCreatureType
        );

        // Assert
        Assert.Empty(result.Monsters);
    }

    [Fact]
    public void Fill_GeneratesExactlyTheGapToMaxPopulation()
    {
        // Act
        var result = CreatureSpawnFiller.Fill(
            _creatureGenerator,
            _archetypeCreatureTypes,
            currentPopulation: 1,
            maxPopulation: 4,
            playerLevel: 1,
            _worldId,
            _locationId,
            _spawnerId,
            _factionsByCreatureType
        );

        // Assert
        Assert.Equal(3, result.Monsters.Count);
    }

    [Fact]
    public void Fill_TagsEveryGeneratedCreature_WithTheSpawnerId()
    {
        // Act
        var result = CreatureSpawnFiller.Fill(
            _creatureGenerator,
            _archetypeCreatureTypes,
            currentPopulation: 0,
            maxPopulation: 3,
            playerLevel: 1,
            _worldId,
            _locationId,
            _spawnerId,
            _factionsByCreatureType
        );

        // Assert
        Assert.All(result.Monsters, m => Assert.Equal(_spawnerId, m.Creature.SpawnerId));
    }

    [Fact]
    public void Fill_PlacesEveryMonster_AtTheGivenLocation()
    {
        // Act
        var result = CreatureSpawnFiller.Fill(
            _creatureGenerator,
            _archetypeCreatureTypes,
            currentPopulation: 0,
            maxPopulation: 3,
            playerLevel: 1,
            _worldId,
            _locationId,
            _spawnerId,
            _factionsByCreatureType
        );

        // Assert
        Assert.All(result.Monsters, m => Assert.Equal(_locationId, m.Creature.LocationId));
    }

    [Fact]
    public void Fill_GroupsAllGeneratedMonsters_UnderOneEncounterGroup()
    {
        // Act
        var result = CreatureSpawnFiller.Fill(
            _creatureGenerator,
            _archetypeCreatureTypes,
            currentPopulation: 0,
            maxPopulation: 3,
            playerLevel: 1,
            _worldId,
            _locationId,
            _spawnerId,
            _factionsByCreatureType
        );

        // Assert
        var group = Assert.Single(result.EncounterGroups);
        var memberCreatureIds = result
            .EncounterGroupMembers.Where(member => member.EncounterGroupId == group.Id)
            .Select(member => member.CreatureId)
            .ToArray();
        Assert.Equal(
            result.Monsters.Select(m => m.Creature.Id).OrderBy(id => id),
            memberCreatureIds.OrderBy(id => id)
        );
        var creatureType = Assert.Single(
            result.Monsters.Select(m => m.Creature.CreatureType).Distinct()
        );
        Assert.Equal(_factionsByCreatureType[creatureType].Id, group.FactionId);
    }
}
