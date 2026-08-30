using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;
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
    public void Generate_ProducesExactlyOneGroup()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

            // Assert
            Assert.Single(result.EncounterGroups);
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
    public void Generate_GivesEveryMonster_ADiurnalSleepSchedule()
    {
        for (var i = 0; i < 10; i++)
        {
            // Act
            var result = _wildernessPopulator.Generate(MakeInput());

            // Assert
            Assert.All(
                result.Monsters,
                monster =>
                {
                    var jobs = result
                        .Jobs.Where(job => job.CreatureId == monster.Creature.Id)
                        .ToArray();
                    var sleep = Assert.Single(jobs, job => job.Action == CreatureJobAction.Sleep);
                    var idle = Assert.Single(jobs, job => job.Action == CreatureJobAction.Idle);
                    Assert.Equal(22, sleep.StartHour);
                    Assert.Equal(6, sleep.EndHour);
                    Assert.Equal(6, idle.StartHour);
                    Assert.Equal(22, idle.EndHour);
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
