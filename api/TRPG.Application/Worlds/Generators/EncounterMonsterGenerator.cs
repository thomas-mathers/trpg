using TRPG.Application.GameSessions;

namespace TRPG.Application.Worlds.Generators;

internal static class EncounterMonsterGenerator
{
    public static CreatureGeneratorResult Generate(
        CreatureGenerator creatureGenerator,
        Guid worldId,
        Guid locationId,
        CreatureArchetype archetype,
        int minimumLevel,
        int maximumLevel
    )
    {
        var level = Random.Shared.Next(minimumLevel, maximumLevel + 1);
        var birthYear = GameClock.EpochYear - level;

        var result = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: archetype.CreatureType!.Value,
                Archetype: archetype,
                WorldId: worldId,
                BirthLocationId: locationId,
                MinLevel: level,
                MaxLevel: level,
                MinBirthYear: birthYear,
                MaxBirthYear: birthYear
            )
        );

        if (archetype.HasPotions)
        {
            result = creatureGenerator.AddStartingPotions(result);
        }

        result.Creature.LocationId = locationId;

        return result;
    }
}
