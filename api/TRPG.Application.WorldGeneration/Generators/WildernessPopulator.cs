using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal class WildernessPopulatorInput
{
    public required Guid LocationId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyDictionary<CreatureType, Faction> FactionsByCreatureType { get; init; }
}

internal record WildernessPopulatorResult(
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<EncounterGroup> EncounterGroups,
    IReadOnlyList<EncounterGroupMember> EncounterGroupMembers,
    CreatureSpawner Spawner
);

public class WildernessPopulator(CreatureGenerator creatureGenerator)
{
    private const int MinimumPopulation = 1;
    private const int MaximumPopulation = 3;
    private const int DefaultSpawnerTriggerHour = 0;

    private static readonly CreatureArchetype[] Archetypes =
    [
        CreatureArchetype.Beast,
        CreatureArchetype.Goblin,
    ];

    private static readonly IReadOnlyList<CreatureType> ArchetypeCreatureTypes = Archetypes
        .Select(archetype => archetype.CreatureType!.Value)
        .ToArray();

    internal WildernessPopulatorResult Generate(WildernessPopulatorInput input)
    {
        var maxPopulation = Random.Shared.Next(MinimumPopulation, MaximumPopulation + 1);
        var spawner = new CreatureSpawner
        {
            WorldId = input.WorldId,
            LocationId = input.LocationId,
            ArchetypeCreatureTypes = ArchetypeCreatureTypes.ToList(),
            MaxPopulation = maxPopulation,
            TriggerHour = DefaultSpawnerTriggerHour,
            SpecificDay = null,
            LastSyncPlaytime = TimeSpan.Zero,
        };

        var fillResult = CreatureSpawnFiller.Fill(
            creatureGenerator,
            ArchetypeCreatureTypes,
            currentPopulation: 0,
            maxPopulation,
            playerLevel: 1,
            input.WorldId,
            input.LocationId,
            spawner.Id,
            input.FactionsByCreatureType
        );

        return new WildernessPopulatorResult(
            fillResult.Monsters,
            fillResult.Jobs,
            fillResult.EncounterGroups,
            fillResult.EncounterGroupMembers,
            spawner
        );
    }
}
