using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public static class MonsterReputationSeeder
{
    private const int BaselineReputation = -10;

    private static readonly IReadOnlyDictionary<
        (Race Race, CreatureType MonsterType),
        int
    > RaceModifiers = new Dictionary<(Race, CreatureType), int>
    {
        [(Race.Orc, CreatureType.Goblin)] = 15,
        [(Race.Dwarf, CreatureType.Construct)] = 15,
        [(Race.Dwarf, CreatureType.Giant)] = -15,
        [(Race.Elf, CreatureType.Beast)] = 15,
    };

    private static readonly IReadOnlyDictionary<
        (PlayerClass PlayerClass, CreatureType MonsterType),
        int
    > ClassModifiers = new Dictionary<(PlayerClass, CreatureType), int>
    {
        [(PlayerClass.Ranger, CreatureType.Beast)] = 15,
        [(PlayerClass.Cleric, CreatureType.Undead)] = -20,
        [(PlayerClass.Cleric, CreatureType.Wraith)] = -20,
        [(PlayerClass.Rogue, CreatureType.Goblin)] = 10,
        [(PlayerClass.Mage, CreatureType.Elemental)] = 15,
    };

    public static IReadOnlyList<Reputation> Seed(
        Guid worldId,
        Guid playerId,
        Race race,
        PlayerClass playerClass,
        IReadOnlyCollection<Faction> factions
    ) =>
        factions
            .Where(faction => faction.CreatureType != null)
            .Select(faction =>
            {
                var monsterType = faction.CreatureType!.Value;
                var score =
                    BaselineReputation
                    + RaceModifiers.GetValueOrDefault((race, monsterType))
                    + ClassModifiers.GetValueOrDefault((playerClass, monsterType));

                return new Reputation
                {
                    WorldId = worldId,
                    CreatureId = playerId,
                    TargetId = faction.Id,
                    TargetType = ReputationTargetType.Faction,
                    Score = score,
                };
            })
            .ToArray();
}
