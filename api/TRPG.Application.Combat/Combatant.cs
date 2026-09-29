using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Domain;
using TRPG.Domain.Models;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;
using ActiveDot = TRPG.Application.Effects.ActiveDot;
using ActiveHot = TRPG.Application.Effects.ActiveHot;
using PersistedCombat = TRPG.Domain.Models;

namespace TRPG.Application.Combat;

public record ConsumableItemSnapshot(
    Guid ItemId,
    string Name,
    ResourceType Resource,
    int Amount,
    int Quantity
)
{
    public static IReadOnlyList<ConsumableItemSnapshot> FromItems(IReadOnlyCollection<Item> items)
    {
        var snapshots = new List<ConsumableItemSnapshot>();

        foreach (var item in items)
        {
            if (item is not Consumable consumable)
            {
                continue;
            }

            snapshots.Add(
                new ConsumableItemSnapshot(
                    item.Id,
                    item.Name,
                    consumable.Resource,
                    consumable.RestoreAmount,
                    item.Quantity
                )
            );
        }

        return snapshots;
    }
}

public class Combatant
{
    public required Guid CreatureId { get; init; }
    public required string Name { get; init; }
    public required bool IsPlayer { get; init; }
    public required CreatureType CreatureType { get; init; }
    public required int Level { get; init; }
    public required Attributes Attributes { get; init; }
    public required IReadOnlyList<Ability> Abilities { get; init; }
    public required int NaturalWeaponMinDamage { get; init; }
    public required int NaturalWeaponMaxDamage { get; init; }
    public required CombatOptions CombatOptions { get; init; }
    public int CurrentHp { get; set; }
    public int CurrentAp { get; set; }
    public int CurrentMp { get; set; }
    public bool IsSurpriseAttacker { get; set; }
    public IReadOnlyList<Item> EquippedItems { get; init; } = [];
    public IReadOnlyList<ConsumableItemSnapshot> ConsumableItemSnapshots { get; init; } = [];
    public Dictionary<WeaponType, int> WeaponProficiencies { get; init; } = [];
    public Dictionary<WeaponType, int> WeaponSwingCounts { get; init; } = [];
    public Dictionary<Skill, int> SkillUsageCounts { get; init; } = [];
    public Dictionary<Guid, int> ItemsUsedCounts { get; init; } = [];
    public Dictionary<ConditionType, GameInstant> ActiveConditions { get; init; } = [];
    public List<ActiveDot> ActiveDots { get; init; } = [];
    public List<ActiveHot> ActiveHots { get; init; } = [];
    public List<ActiveBuff> ActiveBuffs { get; init; } = [];
    public Dictionary<string, GameInstant> CooldownReadyAtByAbility { get; init; } = [];
    public bool IsAlive => CurrentHp > 0;

    public bool IsUnder(ConditionType condition, GameInstant now) =>
        ActiveConditions.TryGetValue(condition, out var expiresAt) && expiresAt > now;

    public bool IsOnCooldown(string abilityName, GameInstant now) =>
        CooldownReadyAtByAbility.TryGetValue(abilityName, out var readyAt) && readyAt > now;

    public int MaximumHp => (int)CalculateEffectiveAttribute(AttributeName.MaximumHp);
    public int MaximumAp => (int)CalculateEffectiveAttribute(AttributeName.MaximumAp);
    public int MaximumMp => (int)CalculateEffectiveAttribute(AttributeName.MaximumMp);
    public float Strength => CalculateEffectiveAttribute(AttributeName.Strength);
    public float Defense => CalculateEffectiveAttribute(AttributeName.Defense);
    public float Dexterity => CalculateEffectiveAttribute(AttributeName.Dexterity);
    public float Stamina => CalculateEffectiveAttribute(AttributeName.Stamina);
    public float Mana => CalculateEffectiveAttribute(AttributeName.Mana);
    public float Intelligence => CalculateEffectiveAttribute(AttributeName.Intelligence);
    public float PhysicalResistance =>
        CalculateEffectiveAttribute(AttributeName.PhysicalResistance);
    public float FireResistance => CalculateEffectiveAttribute(AttributeName.FireResistance);
    public float IceResistance => CalculateEffectiveAttribute(AttributeName.IceResistance);
    public float LightningResistance =>
        CalculateEffectiveAttribute(AttributeName.LightningResistance);
    public float PoisonResistance => CalculateEffectiveAttribute(AttributeName.PoisonResistance);
    public float MagicResistance => CalculateEffectiveAttribute(AttributeName.MagicResistance);
    public float TurnOrder => Dexterity;
    public Weapon? MainHandWeapon =>
        EquippedItems
            .OfType<Weapon>()
            .FirstOrDefault(w => w.Ownership.EquippedSlot == EquipmentSlot.RightHand);
    public Weapon? OffHandWeapon =>
        EquippedItems
            .OfType<Weapon>()
            .FirstOrDefault(w => w.Ownership.EquippedSlot == EquipmentSlot.LeftHand);
    public Shield? Shield => EquippedItems.OfType<Shield>().SingleOrDefault();
    public float BlockChance => Shield?.BlockChance ?? 0f;

    public float Evasion => Dexterity * CombatOptions.EvasionPerDexterityPoint;

    public float AttackRating => AttackRatingFor(MainHandWeapon);

    public float AttackRatingFor(Weapon? weapon)
    {
        if (IsPlayer)
        {
            var weaponProficiency = weapon != null ? WeaponProficiencies[weapon.Type] : 0;
            return CombatOptions.BaseProficiency + weaponProficiency;
        }
        return CombatOptions.NonPlayerProficiencyBase
            + CombatOptions.NonPlayerProficiencyPerLevel * Level;
    }

    public float CritChance =>
        Math.Min(
            CombatOptions.MaxCritChance,
            Dexterity * CombatOptions.CritChancePerDexterityPoint
        );

    public float CritDamageMultiplier => CombatOptions.CritDamageMultiplier;

    public static Combatant FromCreature(
        CombatOptions combatOptions,
        bool isPlayer,
        Creature creature,
        IReadOnlyList<Ability> abilities,
        IReadOnlyList<Item> items,
        IReadOnlyDictionary<WeaponType, int> weaponProficiencies
    )
    {
        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();

        var startingAttributes = StatFormulas.CalculateEffectiveAttributes(
            creature.BaseAttributes,
            [],
            equippedItems
        );

        var combatant = new Combatant
        {
            CreatureId = creature.Id,
            Name = creature.Name,
            IsPlayer = isPlayer,
            CreatureType = creature.CreatureType,
            Level = creature.Level,
            Attributes = creature.BaseAttributes,
            Abilities = abilities,
            NaturalWeaponMinDamage = creature.NaturalWeaponMinDamage,
            NaturalWeaponMaxDamage = creature.NaturalWeaponMaxDamage,
            CombatOptions = combatOptions,
            CurrentHp = Math.Clamp(creature.CurrentHp, 0, startingAttributes.MaximumHp),
            CurrentAp = Math.Min(creature.CurrentAp, startingAttributes.MaximumAp),
            CurrentMp = Math.Min(creature.CurrentMp, startingAttributes.MaximumMp),
            EquippedItems = equippedItems,
            ConsumableItemSnapshots = ConsumableItemSnapshot.FromItems(items),
            WeaponProficiencies = Enum.GetValues<WeaponType>()
                .ToDictionary(type => type, weaponProficiencies.GetValueOrDefault),
            ActiveConditions = creature.ActiveConditions.ToDictionary(
                kv => Enum.Parse<ConditionType>(kv.Key),
                kv => kv.Value
            ),
            CooldownReadyAtByAbility = new Dictionary<string, GameInstant>(
                creature.CooldownReadyAtByAbility
            ),
            ActiveDots = creature
                .ActiveDots.Select(d => new ActiveDot
                {
                    AbilityName = d.AbilityName,
                    Amount = d.Amount,
                    DamageType = Enum.Parse<DamageType>(d.DamageType),
                    NextTickAt = d.NextTickAt,
                    ExpiresAt = d.ExpiresAt,
                })
                .ToList(),
            ActiveHots = creature
                .ActiveHots.Select(h => new ActiveHot
                {
                    AbilityName = h.AbilityName,
                    Amount = h.Amount,
                    NextTickAt = h.NextTickAt,
                    ExpiresAt = h.ExpiresAt,
                })
                .ToList(),
            ActiveBuffs = creature
                .ActiveBuffs.Select(b => new ActiveBuff
                {
                    AbilityName = b.AbilityName,
                    Amount = b.Amount,
                    Attribute = Enum.Parse<AttributeName>(b.Attribute),
                    ExpiresAt = b.ExpiresAt,
                    AmountType = Enum.Parse<AmountType>(b.AmountType),
                })
                .ToList(),
        };

        return combatant;
    }

    public void ApplyTo(Creature creature)
    {
        creature.CurrentHp = CurrentHp;
        creature.CurrentAp = CurrentAp;
        creature.CurrentMp = CurrentMp;
        creature.ActiveConditions = ActiveConditions.ToDictionary(
            kv => kv.Key.ToString(),
            kv => kv.Value
        );
        creature.CooldownReadyAtByAbility = new Dictionary<string, GameInstant>(
            CooldownReadyAtByAbility
        );
        creature.ActiveDots = ActiveDots
            .Select(d => new PersistedCombat.ActiveDot
            {
                AbilityName = d.AbilityName,
                Amount = d.Amount,
                DamageType = d.DamageType.ToString(),
                NextTickAt = d.NextTickAt,
                ExpiresAt = d.ExpiresAt,
            })
            .ToList();
        creature.ActiveHots = ActiveHots
            .Select(h => new PersistedCombat.ActiveHot
            {
                AbilityName = h.AbilityName,
                Amount = h.Amount,
                NextTickAt = h.NextTickAt,
                ExpiresAt = h.ExpiresAt,
            })
            .ToList();
        creature.ActiveBuffs = ActiveBuffs
            .Select(b => new PersistedCombat.ActiveBuff
            {
                AbilityName = b.AbilityName,
                Amount = b.Amount,
                Attribute = b.Attribute.ToString(),
                ExpiresAt = b.ExpiresAt,
                AmountType = b.AmountType.ToString(),
            })
            .ToList();

        StatFormulas.Recalculate(creature, EquippedItems);

        if (!IsAlive)
        {
            creature.Die();
        }
    }

    private float CalculateEffectiveAttribute(AttributeName attribute) =>
        StatFormulas.CalculateEffectiveAttribute(Attributes, ActiveBuffs, EquippedItems, attribute);

    public float ResistanceFor(DamageType damageType) =>
        CalculateEffectiveAttribute(DamageMitigation.ResistanceAttribute(damageType));
}
