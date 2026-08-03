using TRPG.Application.GameSessions;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class DungeonPopulatorInput
{
    public required Guid StateId { get; init; }
    public required Guid RoomId { get; init; }
    public required Guid WorldId { get; init; }
    public required BuildingType DungeonType { get; init; }
}

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

    public IReadOnlyList<CreatureGeneratorResult> Generate(DungeonPopulatorInput input)
    {
        var archetypes = ArchetypesByDungeonType[input.DungeonType];
        var count = Random.Shared.Next(MinimumMonsters, MaximumMonsters + 1);

        var monsters = new List<CreatureGeneratorResult>();
        for (var i = 0; i < count; i++)
        {
            var archetype = archetypes[Random.Shared.Next(archetypes.Length)];
            monsters.Add(GenerateMonster(input, archetype));
        }

        return monsters;
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
                BirthStateId: input.StateId,
                StateId: input.StateId,
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

        result.Creature.RoomId = input.RoomId;

        return result;
    }
}
