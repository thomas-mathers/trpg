using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record MonsterGenerationResult(
    CreatureGeneratorResult Result,
    IReadOnlyList<CreatureJob> Jobs
);

internal static class EncounterMonsterGenerator
{
    private static readonly HourWindow DiurnalSleepHours = new(22, 6);
    private static readonly HourWindow DiurnalIdleHours = new(6, 22);
    private static readonly HourWindow NocturnalSleepHours = new(6, 22);
    private static readonly HourWindow NocturnalIdleHours = new(22, 6);

    private static readonly HashSet<CreatureType> NocturnalTypes =
    [
        CreatureType.Undead,
        CreatureType.Wraith,
        CreatureType.Demon,
    ];

    private static readonly HashSet<CreatureType> NeverSleepsTypes =
    [
        CreatureType.Construct,
        CreatureType.Elemental,
    ];

    public static MonsterGenerationResult Generate(
        CreatureGenerator creatureGenerator,
        Guid worldId,
        Guid locationId,
        CreatureArchetype archetype,
        int minimumLevel,
        int maximumLevel
    )
    {
        var level = Random.Shared.Next(minimumLevel, maximumLevel + 1);
        var birthYear = WorldEpoch.Year - level;

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

        var jobs = GenerateSleepSchedule(
            result.Creature.Id,
            locationId,
            worldId,
            archetype.CreatureType.Value
        );

        return new MonsterGenerationResult(result, jobs);
    }

    private static IReadOnlyList<CreatureJob> GenerateSleepSchedule(
        Guid creatureId,
        Guid locationId,
        Guid worldId,
        CreatureType creatureType
    )
    {
        if (NeverSleepsTypes.Contains(creatureType))
        {
            return [];
        }

        var (sleepHours, idleHours) = NocturnalTypes.Contains(creatureType)
            ? (NocturnalSleepHours, NocturnalIdleHours)
            : (DiurnalSleepHours, DiurnalIdleHours);

        return
        [
            CreatureJobGenerator.GenerateSleep(creatureId, locationId, worldId, sleepHours),
            CreatureJobGenerator.GenerateIdle(creatureId, locationId, worldId, idleHours),
        ];
    }
}
