using TRPG.Application.GameSessions;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class DungeonPopulatorInput
{
    public required Guid LocationId { get; init; }
    public required Guid WorldId { get; init; }
    public required BuildingType DungeonType { get; init; }
    public required IReadOnlyDictionary<CreatureType, Faction> FactionsByCreatureType { get; init; }
}

public record DungeonPopulatorResult(
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<EncounterGroup> EncounterGroups,
    IReadOnlyList<EncounterGroupMember> EncounterGroupMembers
);

public class DungeonPopulator(CreatureGenerator creatureGenerator)
{
    private const int MinimumMonsters = 1;
    private const int MaximumMonsters = 3;
    private const int MinimumLevel = 1;
    private const int MaximumLevel = 3;

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

    public DungeonPopulatorResult Generate(DungeonPopulatorInput input)
    {
        var archetypes = ArchetypesByDungeonType[input.DungeonType];
        var count = Random.Shared.Next(MinimumMonsters, MaximumMonsters + 1);

        var monsters = new List<CreatureGeneratorResult>();
        for (var i = 0; i < count; i++)
        {
            var archetype = archetypes[Random.Shared.Next(archetypes.Length)];
            monsters.Add(GenerateMonster(input, archetype));
        }

        var groups = CreateGroups(monsters, input);

        return new DungeonPopulatorResult(
            monsters,
            groups.Select(group => group.Group).ToArray(),
            groups.SelectMany(group => group.Members).ToArray()
        );
    }

    private static IReadOnlyList<EncounterGroupWithMembers> CreateGroups(
        IReadOnlyList<CreatureGeneratorResult> monsters,
        DungeonPopulatorInput input
    ) =>
        monsters
            .GroupBy(monster => monster.Creature.CreatureType)
            .Select(group => CreateGroup(group, input))
            .ToArray();

    private static EncounterGroupWithMembers CreateGroup(
        IGrouping<CreatureType, CreatureGeneratorResult> monsters,
        DungeonPopulatorInput input
    )
    {
        var group = new EncounterGroup
        {
            WorldId = input.WorldId,
            LocationId = input.LocationId,
            FactionId = input.FactionsByCreatureType[monsters.Key].Id,
        };
        var members = monsters
            .Select(monster => new EncounterGroupMember
            {
                WorldId = input.WorldId,
                EncounterGroupId = group.Id,
                CreatureId = monster.Creature.Id,
            })
            .ToArray();

        return new EncounterGroupWithMembers(group, members);
    }

    private CreatureGeneratorResult GenerateMonster(
        DungeonPopulatorInput input,
        CreatureArchetype archetype
    )
    {
        var level = Random.Shared.Next(MinimumLevel, MaximumLevel + 1);
        var birthYear = GameClock.EpochYear - level;

        var result = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: archetype.CreatureType!.Value,
                Archetype: archetype,
                WorldId: input.WorldId,
                BirthLocationId: input.LocationId,
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

        result.Creature.LocationId = input.LocationId;

        return result;
    }
}

public record EncounterGroupWithMembers(
    EncounterGroup Group,
    IReadOnlyList<EncounterGroupMember> Members
);
