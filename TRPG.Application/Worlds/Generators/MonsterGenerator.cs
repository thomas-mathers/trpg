using TRPG.Application.Creatures;
using TRPG.Application.Game;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal class MonsterGeneratorInput
{
    public required Guid StateId { get; init; }
    public required Guid RoomId { get; init; }
    public required Guid WorldId { get; init; }
    public required BuildingType DungeonType { get; init; }
}

internal static class MonsterGenerator
{
    private const int MinimumMonsters = 1;
    private const int MaximumMonsters = 3;
    private const int MinimumLevel = 3;
    private const int MaximumLevel = 12;

    private static readonly Dictionary<BuildingType, CreatureType[]> MonsterTypesByDungeonType =
        new()
        {
            [BuildingType.Cave] = [CreatureType.Beast],
            [BuildingType.Crypt] = [CreatureType.Undead],
            [BuildingType.Mine] = [CreatureType.Construct, CreatureType.Beast],
            [BuildingType.Ruins] = [CreatureType.Undead, CreatureType.Demon],
            [BuildingType.Tower] = [CreatureType.Elemental, CreatureType.Demon],
        };

    private static readonly Dictionary<CreatureType, string> Descriptions = new()
    {
        [CreatureType.Beast] = "A feral creature of claw and hunger, hostile to intruders.",
        [CreatureType.Undead] = "A restless corpse animated by something that is not life.",
        [CreatureType.Construct] = "An artificial thing still obeying an order given long ago.",
        [CreatureType.Demon] = "A malevolent entity from somewhere that is not this world.",
        [CreatureType.Elemental] = "Raw elemental force bound loosely into a walking shape.",
    };

    public static IReadOnlyList<CreatureGeneratorResult> Generate(MonsterGeneratorInput input)
    {
        var monsterTypes = MonsterTypesByDungeonType[input.DungeonType];
        var count = Random.Shared.Next(MinimumMonsters, MaximumMonsters + 1);

        var monsters = new List<CreatureGeneratorResult>();
        for (var i = 0; i < count; i++)
        {
            var creatureType = monsterTypes[Random.Shared.Next(monsterTypes.Length)];
            monsters.Add(GenerateMonster(input, creatureType));
        }

        return monsters;
    }

    private static CreatureGeneratorResult GenerateMonster(
        MonsterGeneratorInput input,
        CreatureType creatureType
    )
    {
        var level = Random.Shared.Next(MinimumLevel, MaximumLevel + 1);
        var gender = Random.Shared.Next(2) == 0 ? Gender.Male : Gender.Female;

        var attributes = GetAttributes(level);

        var creature = new Creature
        {
            WorldId = input.WorldId,
            Name = CreatureGenerator.GetName(creatureType, gender),
            CreatureType = creatureType,
            Gender = gender,
            Profession = null,
            BirthStateId = input.StateId,
            BirthYear = GameClock.EpochYear - level,
            StateId = input.StateId,
            RoomId = input.RoomId,
            Level = level,
            Biography = Descriptions[creatureType],
            Attributes = attributes,
            CurrentHp = attributes.MaximumHp,
            CurrentAp = attributes.MaximumAp,
            CurrentMp = attributes.MaximumMp,
            LastRegenPlaytime = TimeSpan.Zero,
        };

        return new CreatureGeneratorResult(creature, [], [], [], []);
    }

    private static Attributes GetAttributes(int level)
    {
        var baseAttributes = new Attributes
        {
            Strength = 4 + level,
            Dexterity = 4 + level,
            Intelligence = 1,
            Endurance = 4 + level,
            Stamina = 4 + level,
            Defense = 2 + level,
            Mana = 1,
            MovementSpeed = 1.0f,
        };

        return baseAttributes with
        {
            MaximumHp = StatFormulas.CalculateMaximumHp(baseAttributes),
            MaximumAp = StatFormulas.CalculateMaximumAp(baseAttributes),
            MaximumMp = StatFormulas.CalculateMaximumMp(baseAttributes),
        };
    }
}
