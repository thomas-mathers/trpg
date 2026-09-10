using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

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
    IReadOnlyList<FactionMember> FactionMembers,
    CreatureSpawner Spawner
);

public class DungeonPopulator(CreatureGenerator creatureGenerator)
{
    private const int MinimumPopulation = 1;
    private const int MaximumPopulation = 3;

    // Clearing a room should mean something for more than a night, and staggering the hour keeps a
    // whole dungeon from repopulating on a single tick.
    private const int RespawnIntervalDays = 2;

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

    // A KeyLock guard or Miniboss is a forced, single occupant rather than the usual random
    // 0-3 population roll — the room is always occupied, never sometimes empty. Both reuse this
    // one method: the guard passes the normal player level, Miniboss passes an elevated one, and
    // the level math itself (CreatureSpawnFiller's spawn-level curve) needs no changes either way.
    internal DungeonPopulatorResult GenerateForced(
        Guid worldId,
        Guid locationId,
        BuildingType dungeonType,
        int playerLevel,
        IReadOnlyDictionary<CreatureType, Faction> factionsByCreatureType
    )
    {
        var archetypeCreatureTypes = ArchetypesByDungeonType[dungeonType]
            .Select(archetype => archetype.CreatureType!.Value)
            .ToArray();

        var spawner = new CreatureSpawner
        {
            WorldId = worldId,
            LocationId = locationId,
            ArchetypeCreatureTypes = archetypeCreatureTypes.ToList(),
            MaxPopulation = 1,
            Schedule = $"0 {Random.Shared.Next(24)} */{RespawnIntervalDays} * *",
            LastSyncPlaytime = TimeSpan.Zero,
        };

        var fillResult = CreatureSpawnFiller.Fill(
            creatureGenerator,
            archetypeCreatureTypes,
            currentPopulation: 0,
            maxPopulation: 1,
            playerLevel,
            worldId,
            locationId,
            spawner.Id,
            factionsByCreatureType
        );

        return new DungeonPopulatorResult(
            fillResult.Monsters,
            fillResult.Jobs,
            fillResult.EncounterGroups,
            fillResult.EncounterGroupMembers,
            fillResult.FactionMembers,
            spawner
        );
    }

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
            Schedule = $"0 {Random.Shared.Next(24)} */{RespawnIntervalDays} * *",
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
            fillResult.FactionMembers,
            spawner
        );
    }
}
