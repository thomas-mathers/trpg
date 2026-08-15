using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class WildernessPopulatorInput
{
    public required Guid LocationId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyDictionary<CreatureType, Faction> FactionsByCreatureType { get; init; }
}

public record WildernessPopulatorResult(
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<EncounterGroup> EncounterGroups,
    IReadOnlyList<EncounterGroupMember> EncounterGroupMembers
);

public class WildernessPopulator(CreatureGenerator creatureGenerator)
{
    private const int MinimumGroups = 0;
    private const int MaximumGroups = 2;
    private const int MinimumMonsters = 1;
    private const int MaximumMonsters = 3;
    private const int MinimumLevel = 1;
    private const int MaximumLevel = 3;

    private static readonly CreatureArchetype[] Archetypes =
    [
        CreatureArchetype.Beast,
        CreatureArchetype.Goblin,
    ];

    public WildernessPopulatorResult Generate(WildernessPopulatorInput input)
    {
        var groupCount = Random.Shared.Next(MinimumGroups, MaximumGroups + 1);

        var monsters = new List<CreatureGeneratorResult>();
        var jobs = new List<CreatureJob>();
        var groups = new List<EncounterGroup>();
        var members = new List<EncounterGroupMember>();

        for (var i = 0; i < groupCount; i++)
        {
            var group = GenerateGroup(input);
            monsters.AddRange(group.Monsters);
            jobs.AddRange(group.Jobs);
            groups.Add(group.Group);
            members.AddRange(group.Members);
        }

        return new WildernessPopulatorResult(monsters, jobs, groups, members);
    }

    private WildernessGroup GenerateGroup(WildernessPopulatorInput input)
    {
        var archetype = Archetypes[Random.Shared.Next(Archetypes.Length)];
        var count = Random.Shared.Next(MinimumMonsters, MaximumMonsters + 1);

        var generated = new List<MonsterGenerationResult>();
        for (var i = 0; i < count; i++)
        {
            generated.Add(
                EncounterMonsterGenerator.Generate(
                    creatureGenerator,
                    input.WorldId,
                    input.LocationId,
                    archetype,
                    MinimumLevel,
                    MaximumLevel
                )
            );
        }

        var monsters = generated.Select(g => g.Result).ToArray();
        var jobs = generated.SelectMany(g => g.Jobs).ToArray();

        var group = new EncounterGroup
        {
            WorldId = input.WorldId,
            LocationId = input.LocationId,
            FactionId = input.FactionsByCreatureType[archetype.CreatureType!.Value].Id,
        };
        var members = monsters
            .Select(monster => new EncounterGroupMember
            {
                WorldId = input.WorldId,
                EncounterGroupId = group.Id,
                CreatureId = monster.Creature.Id,
            })
            .ToArray();

        return new WildernessGroup(monsters, jobs, group, members);
    }
}

internal record WildernessGroup(
    IReadOnlyList<CreatureGeneratorResult> Monsters,
    IReadOnlyList<CreatureJob> Jobs,
    EncounterGroup Group,
    IReadOnlyList<EncounterGroupMember> Members
);
