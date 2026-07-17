using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatantMappers
{
    public static CombatantSnapshot ToSnapshot(this Combatant combatant, Guid sessionId) =>
        new()
        {
            SessionId = sessionId,
            CreatureId = combatant.CreatureId,
            IsPlayer = combatant.IsPlayer,
            CurrentHp = combatant.CurrentHp,
            CurrentAp = combatant.CurrentAp,
            CurrentMp = combatant.CurrentMp,
            WeaponSwingCounts = combatant.WeaponSwingCounts.ToDictionary(
                kv => kv.Key.ToString(),
                kv => kv.Value
            ),
            ActiveConditions = combatant.ActiveConditions.ToDictionary(
                kv => kv.Key.ToString(),
                kv => kv.Value
            ),
            CooldownRemainingByAbility = new Dictionary<string, int>(
                combatant.CooldownRemainingByAbility
            ),
            ActiveDots = combatant
                .ActiveDots.Select(d => new ActiveDotSnapshot
                {
                    AbilityName = d.AbilityName,
                    Amount = d.Amount,
                    DamageType = d.DamageType.ToString(),
                    RemainingTurns = d.RemainingTurns,
                })
                .ToList(),
            ActiveHots = combatant
                .ActiveHots.Select(h => new ActiveHotSnapshot
                {
                    AbilityName = h.AbilityName,
                    Amount = h.Amount,
                    RemainingTurns = h.RemainingTurns,
                })
                .ToList(),
            ActiveBuffs = combatant
                .ActiveBuffs.Select(b => new ActiveBuffSnapshot
                {
                    Amount = b.Amount,
                    Attribute = b.Attribute.ToString(),
                    RemainingTurns = b.RemainingTurns,
                    AmountType = b.AmountType.ToString(),
                })
                .ToList(),
        };

    public static Combatant ToCombatant(
        this CombatantSnapshot snapshot,
        Creature creature,
        IReadOnlyList<Ability> abilities,
        IReadOnlyList<Item> inventory,
        Dictionary<WeaponType, int> weaponProficiencies
    ) =>
        new()
        {
            CreatureId = creature.Id,
            Name = creature.Name,
            IsPlayer = snapshot.IsPlayer,
            Level = creature.Level,
            Attributes = creature.Attributes,
            Abilities = abilities,
            Gold = creature.Gold,
            CurrentHp = snapshot.CurrentHp,
            CurrentAp = snapshot.CurrentAp,
            CurrentMp = snapshot.CurrentMp,
            Inventory = inventory,
            WeaponProficiencies = weaponProficiencies,
            WeaponSwingCounts = snapshot.WeaponSwingCounts.ToDictionary(
                kv => Enum.Parse<WeaponType>(kv.Key),
                kv => kv.Value
            ),
            ActiveConditions = snapshot.ActiveConditions.ToDictionary(
                kv => Enum.Parse<ConditionType>(kv.Key),
                kv => kv.Value
            ),
            CooldownRemainingByAbility = new Dictionary<string, int>(
                snapshot.CooldownRemainingByAbility
            ),
            ActiveDots = snapshot
                .ActiveDots.Select(d => new ActiveDot
                {
                    AbilityName = d.AbilityName,
                    Amount = d.Amount,
                    DamageType = Enum.Parse<DamageType>(d.DamageType),
                    RemainingTurns = d.RemainingTurns,
                })
                .ToList(),
            ActiveHots = snapshot
                .ActiveHots.Select(h => new ActiveHot
                {
                    AbilityName = h.AbilityName,
                    Amount = h.Amount,
                    RemainingTurns = h.RemainingTurns,
                })
                .ToList(),
            ActiveBuffs = snapshot
                .ActiveBuffs.Select(b => new ActiveBuff
                {
                    Amount = b.Amount,
                    Attribute = Enum.Parse<AttributeName>(b.Attribute),
                    RemainingTurns = b.RemainingTurns,
                    AmountType = Enum.Parse<AmountType>(b.AmountType),
                })
                .ToList(),
        };
}
