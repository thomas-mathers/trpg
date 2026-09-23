using TRPG.Application.WorldGeneration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class MonsterReputationSeederTests
{
    [Fact]
    public void Seed_CreatesRaceAndClassModifiedReputation_ForEveryMonsterFaction()
    {
        var worldId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var monsterFactions = EncounterFactionGenerator.Generate(worldId);
        var namedFaction = new Faction { WorldId = worldId };

        var reputations = MonsterReputationSeeder.Seed(
            worldId,
            playerId,
            Race.Orc,
            PlayerClass.Rogue,
            [.. monsterFactions.Values, namedFaction]
        );

        Assert.Equal(monsterFactions.Count, reputations.Count);
        Assert.All(reputations, reputation => Assert.Equal(playerId, reputation.CreatureId));
        Assert.All(
            reputations,
            reputation => Assert.Equal(ReputationTargetType.Faction, reputation.TargetType)
        );
        var goblinReputation = reputations.Single(reputation =>
            reputation.TargetId == monsterFactions[CreatureType.Goblin].Id
        );
        Assert.Equal(15, goblinReputation.Score);
        var beastReputation = reputations.Single(reputation =>
            reputation.TargetId == monsterFactions[CreatureType.Beast].Id
        );
        Assert.Equal(-10, beastReputation.Score);
    }

    [Fact]
    public void Generate_GivesOnlySocialMonsterFactionsReputationSensitivity()
    {
        var factions = EncounterFactionGenerator.Generate(Guid.NewGuid());

        Assert.Equal(0, factions[CreatureType.Beast].ReputationSensitivity);
        Assert.Equal(0, factions[CreatureType.Undead].ReputationSensitivity);
        Assert.Equal(0, factions[CreatureType.Wraith].ReputationSensitivity);
        Assert.Equal(0, factions[CreatureType.Construct].ReputationSensitivity);
        Assert.Equal(0, factions[CreatureType.Elemental].ReputationSensitivity);
        Assert.True(factions[CreatureType.Goblin].ReputationSensitivity > 0);
        Assert.True(factions[CreatureType.Giant].ReputationSensitivity > 0);
        Assert.True(factions[CreatureType.Dragon].ReputationSensitivity > 0);
        Assert.True(factions[CreatureType.Demon].ReputationSensitivity > 0);
    }

    [Fact]
    public void Generate_AssignsAttackApproachToEveryMonsterFaction()
    {
        var factions = EncounterFactionGenerator.Generate(Guid.NewGuid());

        Assert.All(
            factions.Values,
            faction => Assert.Equal(EncounterApproach.Attack, faction.EncounterApproach)
        );
    }
}
