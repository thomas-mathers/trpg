using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

internal static class CombatantMapper
{
    public static CombatantSnapshot ToRow(Combatant combatant, Guid sessionId) =>
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
        CombatantSnapshot row,
        Creature creature,
        IReadOnlyList<Ability> abilities,
        IReadOnlyList<Item> inventory,
        Dictionary<WeaponType, int> weaponProficiencies
    ) =>
        new()
        {
            CreatureId = creature.Id,
            Name = creature.Name,
            IsPlayer = row.IsPlayer,
            Level = creature.Level,
            Attributes = creature.Attributes,
            Abilities = abilities,
            Gold = creature.Gold,
            CurrentHp = row.CurrentHp,
            CurrentAp = row.CurrentAp,
            CurrentMp = row.CurrentMp,
            Inventory = inventory,
            WeaponProficiencies = weaponProficiencies,
            WeaponSwingCounts = row.WeaponSwingCounts.ToDictionary(
                kv => Enum.Parse<WeaponType>(kv.Key),
                kv => kv.Value
            ),
            ActiveConditions = row.ActiveConditions.ToDictionary(
                kv => Enum.Parse<ConditionType>(kv.Key),
                kv => kv.Value
            ),
            CooldownRemainingByAbility = new Dictionary<string, int>(
                row.CooldownRemainingByAbility
            ),
            ActiveDots = row
                .ActiveDots.Select(d => new ActiveDot
                {
                    AbilityName = d.AbilityName,
                    Amount = d.Amount,
                    DamageType = Enum.Parse<DamageType>(d.DamageType),
                    RemainingTurns = d.RemainingTurns,
                })
                .ToList(),
            ActiveHots = row
                .ActiveHots.Select(h => new ActiveHot
                {
                    AbilityName = h.AbilityName,
                    Amount = h.Amount,
                    RemainingTurns = h.RemainingTurns,
                })
                .ToList(),
            ActiveBuffs = row
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
