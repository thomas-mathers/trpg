using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class WildernessPopulatorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly WildernessPopulator _wildernessPopulator = new(
        Builders.MakeCreatureGenerator()
    );
    private readonly IReadOnlyDictionary<CreatureType, Faction> _factionsByCreatureType =
        EncounterFactionGenerator.Generate(Guid.NewGuid());

    private WildernessPopulatorInput MakeInput() =>
        new()
        {
            LocationId = _locationId,
            WorldId = _worldId,
            FactionsByCreatureType = _factionsByCreatureType,
        };

    [Fact]
    public void Generate_ProducesBetweenZeroAndTwoGroups()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

            // Assert
            Assert.InRange(result.EncounterGroups.Count, 0, 2);
        }
    }

    [Fact]
    public void Generate_ThemesEveryMonster_AsBeastsOrGoblins()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

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
    public void Generate_PlacesEveryMonster_AtTheGivenLocation()
    {
        for (var i = 0; i < 10; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

            // Assert
            Assert.All(result.Monsters, m => Assert.Equal(_locationId, m.Creature.LocationId));
        }
    }

    [Fact]
    public void Generate_EachGroupHasOneToThreeMembers_OfASingleCreatureType()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

            // Assert
            Assert.All(
                result.EncounterGroups,
                group =>
                {
                    var memberCreatureIds = result
                        .EncounterGroupMembers.Where(member => member.EncounterGroupId == group.Id)
                        .Select(member => member.CreatureId)
                        .ToArray();
                    Assert.InRange(memberCreatureIds.Length, 1, 3);

                    var creatureType = Assert.Single(
                        result
                            .Monsters.Where(monster =>
                                memberCreatureIds.Contains(monster.Creature.Id)
                            )
                            .Select(monster => monster.Creature.CreatureType)
                            .Distinct()
                    );
                    Assert.Equal(_factionsByCreatureType[creatureType].Id, group.FactionId);
                }
            );
        }
    }

    [Fact]
    public void Generate_EveryMonster_BelongsToExactlyOneGroup()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

            // Assert
            Assert.Equal(
                result.Monsters.Select(monster => monster.Creature.Id).OrderBy(id => id),
                result.EncounterGroupMembers.Select(member => member.CreatureId).OrderBy(id => id)
            );
        }
    }
}
