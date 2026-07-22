using TRPG.Application.Creatures;
using TRPG.Application.GameSessions;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class MonsterGeneratorInput
{
    public required Guid StateId { get; init; }
    public required Guid RoomId { get; init; }
    public required Guid WorldId { get; init; }
    public required BuildingType DungeonType { get; init; }
}

public class MonsterGenerator(StatFormulas statFormulas)
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

    public static bool SupportsDungeonType(BuildingType buildingType) =>
        MonsterTypesByDungeonType.ContainsKey(buildingType);

    public IReadOnlyList<CreatureGeneratorResult> Generate(MonsterGeneratorInput input)
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

    private CreatureGeneratorResult GenerateMonster(
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
            Experience = SkillFormulas.XpForCharacterLevel(level),
            Biography = Descriptions[creatureType],
            BaseAttributes = attributes,
            LastRegenPlaytime = TimeSpan.Zero,
        };

        CreatureAttributesRecalculator.Recalculate(creature, []);
        creature.CurrentHp = creature.MaximumHp;
        creature.CurrentAp = creature.MaximumAp;
        creature.CurrentMp = creature.MaximumMp;

        return new CreatureGeneratorResult(creature, [], [], [], []);
    }

    private Attributes GetAttributes(int level)
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
            MaximumHp = statFormulas.CalculateMaximumHp(baseAttributes),
            MaximumAp = statFormulas.CalculateMaximumAp(baseAttributes),
            MaximumMp = statFormulas.CalculateMaximumMp(baseAttributes),
        };
    }
}
