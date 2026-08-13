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
        var archetype = archetypes[Random.Shared.Next(archetypes.Length)];
        var count = Random.Shared.Next(MinimumMonsters, MaximumMonsters + 1);

        var monsters = new List<CreatureGeneratorResult>();
        for (var i = 0; i < count; i++)
        {
            monsters.Add(GenerateMonster(input, archetype));
        }

        var group = CreateGroup(monsters, archetype.CreatureType!.Value, input);

        return new DungeonPopulatorResult(monsters, [group.Group], group.Members);
    }

    private static EncounterGroupWithMembers CreateGroup(
        IReadOnlyList<CreatureGeneratorResult> monsters,
        CreatureType creatureType,
        DungeonPopulatorInput input
    )
    {
        var group = new EncounterGroup
        {
            WorldId = input.WorldId,
            LocationId = input.LocationId,
            FactionId = input.FactionsByCreatureType[creatureType].Id,
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
    ) =>
        EncounterMonsterGenerator.Generate(
            creatureGenerator,
            input.WorldId,
            input.LocationId,
            archetype,
            MinimumLevel,
            MaximumLevel
        );
}

public record EncounterGroupWithMembers(
    EncounterGroup Group,
    IReadOnlyList<EncounterGroupMember> Members
);
