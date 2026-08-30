using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

public record CreatureSpawnFillResult(
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<EncounterGroup> EncounterGroups,
    IReadOnlyList<EncounterGroupMember> EncounterGroupMembers
);

public static class CreatureSpawnFiller
{
    public static CreatureSpawnFillResult Fill(
        CreatureGenerator creatureGenerator,
        IReadOnlyList<CreatureType> archetypeCreatureTypes,
        int currentPopulation,
        int maxPopulation,
        int playerLevel,
        Guid worldId,
        Guid locationId,
        Guid spawnerId,
        IReadOnlyDictionary<CreatureType, Faction> factionsByCreatureType
    )
    {
        var missing = maxPopulation - currentPopulation;
        if (missing <= 0 || archetypeCreatureTypes.Count == 0)
        {
            return new CreatureSpawnFillResult([], [], [], []);
        }

        var creatureType = archetypeCreatureTypes[Random.Shared.Next(archetypeCreatureTypes.Count)];
        var archetype = CreatureArchetype.For(creatureType);
        var (minimumLevel, maximumLevel) = LevelRange(creatureType, playerLevel);

        var generated = new List<MonsterGenerationResult>();
        for (var i = 0; i < missing; i++)
        {
            var monster = EncounterMonsterGenerator.Generate(
                creatureGenerator,
                worldId,
                locationId,
                archetype,
                minimumLevel,
                maximumLevel
            );
            monster.Result.Creature.SpawnerId = spawnerId;
            generated.Add(monster);
        }

        var monsters = generated.Select(g => g.Result).ToArray();
        var jobs = generated.SelectMany(g => g.Jobs).ToArray();

        var group = new EncounterGroup
        {
            WorldId = worldId,
            LocationId = locationId,
            FactionId = factionsByCreatureType[creatureType].Id,
        };
        var members = monsters
            .Select(monster => new EncounterGroupMember
            {
                WorldId = worldId,
                EncounterGroupId = group.Id,
                CreatureId = monster.Creature.Id,
            })
            .ToArray();

        return new CreatureSpawnFillResult(monsters, jobs, [group], members);
    }

    private static (int MinimumLevel, int MaximumLevel) LevelRange(
        CreatureType creatureType,
        int playerLevel
    )
    {
        var spawnLevel = CreatureLevelScaling.SpawnLevel(creatureType, playerLevel);
        return (Math.Max(1, spawnLevel - 1), spawnLevel + 1);
    }
}
