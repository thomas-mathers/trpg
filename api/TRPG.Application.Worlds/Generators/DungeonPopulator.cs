using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal class DungeonPopulatorInput
{
    public required Guid LocationId { get; init; }
    public required Guid WorldId { get; init; }
    public required BuildingType DungeonType { get; init; }
    public required IReadOnlyDictionary<CreatureType, Faction> FactionsByCreatureType { get; init; }
}

internal record DungeonPopulatorResult(
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<EncounterGroup> EncounterGroups,
    IReadOnlyList<EncounterGroupMember> EncounterGroupMembers,
    CreatureSpawner Spawner
);

public class DungeonPopulator(CreatureGenerator creatureGenerator)
{
    private const int MinimumPopulation = 1;
    private const int MaximumPopulation = 3;
    private const int DefaultSpawnerTriggerHour = 0;

    private static readonly Dictionary<BuildingType, CreatureArchetype[]> ArchetypesByDungeonType =
        new()
        {
            [BuildingType.Cave] = [CreatureArchetype.Beast, CreatureArchetype.Goblin],
            [BuildingType.Crypt] = [CreatureArchetype.Undead, CreatureArchetype.Wraith],
            [BuildingType.Mine] =
            [
                CreatureArchetype.Construct,
                CreatureArchetype.Beast,
                CreatureArchetype.Goblin,
            ],
            [BuildingType.Ruins] =
            [
                CreatureArchetype.Undead,
                CreatureArchetype.Demon,
                CreatureArchetype.Giant,
            ],
            [BuildingType.Tower] =
            [
                CreatureArchetype.Elemental,
                CreatureArchetype.Demon,
                CreatureArchetype.Dragon,
            ],
        };

    public static bool SupportsDungeonType(BuildingType buildingType) =>
        ArchetypesByDungeonType.ContainsKey(buildingType);

    internal DungeonPopulatorResult Generate(DungeonPopulatorInput input)
    {
        var archetypeCreatureTypes = ArchetypesByDungeonType[input.DungeonType]
            .Select(archetype => archetype.CreatureType!.Value)
            .ToArray();
        var maxPopulation = Random.Shared.Next(MinimumPopulation, MaximumPopulation + 1);

        var spawner = new CreatureSpawner
        {
            WorldId = input.WorldId,
            LocationId = input.LocationId,
            ArchetypeCreatureTypes = archetypeCreatureTypes.ToList(),
            MaxPopulation = maxPopulation,
            TriggerHour = DefaultSpawnerTriggerHour,
            SpecificDay = null,
            LastSyncPlaytime = TimeSpan.Zero,
        };

        var fillResult = CreatureSpawnFiller.Fill(
            creatureGenerator,
            archetypeCreatureTypes,
            currentPopulation: 0,
            maxPopulation,
            playerLevel: 1,
            input.WorldId,
            input.LocationId,
            spawner.Id,
            input.FactionsByCreatureType
        );

        return new DungeonPopulatorResult(
            fillResult.Monsters,
            fillResult.Jobs,
            fillResult.EncounterGroups,
            fillResult.EncounterGroupMembers,
            spawner
        );
    }
}
