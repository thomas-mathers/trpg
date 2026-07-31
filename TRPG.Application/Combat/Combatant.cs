using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Data.Models;
using ActiveBuff = TRPG.Application.Creatures.ActiveBuff;
using PersistedCombat = TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class ActiveDot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public DamageType DamageType { get; init; }
    public int RemainingTurns { get; set; }
}

public class ActiveHot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public int RemainingTurns { get; set; }
}

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
    public IReadOnlyList<Item> EquippedItems { get; init; } = [];
    public IReadOnlyList<ConsumableItemSnapshot> ConsumableItemSnapshots { get; init; } = [];
    public Dictionary<WeaponType, int> WeaponProficiencies { get; init; } = [];
    public Dictionary<WeaponType, int> WeaponSwingCounts { get; init; } = [];
    public Dictionary<Skill, int> SkillUsageCounts { get; init; } = [];
    public Dictionary<Guid, int> ItemsUsedCounts { get; init; } = [];
    public Dictionary<ConditionType, int> ActiveConditions { get; init; } = [];
    public List<ActiveDot> ActiveDots { get; init; } = [];
    public List<ActiveHot> ActiveHots { get; init; } = [];
    public List<ActiveBuff> ActiveBuffs { get; init; } = [];
    public Dictionary<string, int> CooldownRemainingByAbility { get; init; } = [];
    public bool IsAlive => CurrentHp > 0;
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
    public Weapon? Weapon => EquippedItems.OfType<Weapon>().SingleOrDefault();
    public Shield? Shield => EquippedItems.OfType<Shield>().SingleOrDefault();
    public float BlockChance => Shield?.BlockChance ?? 0f;

    public float Evasion => Dexterity * CombatOptions.EvasionPerDexterityPoint;

    public float AttackRating
    {
        get
        {
            if (IsPlayer)
            {
                var weaponProficiency = Weapon != null ? WeaponProficiencies[Weapon.Type] : 0;
                return CombatOptions.BaseProficiency + weaponProficiency;
            }
            return CombatOptions.NonPlayerProficiencyBase
                + CombatOptions.NonPlayerProficiencyPerLevel * Level;
        }
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
            ActiveConditions = Enum.GetValues<ConditionType>()
                .ToDictionary(
                    condition => condition,
                    condition =>
                        creature.ActiveConditions.GetValueOrDefault(condition.ToString(), 0)
                ),
            CooldownRemainingByAbility = abilities.ToDictionary(
                ability => ability.Name,
                ability => creature.CooldownRemainingByAbility.GetValueOrDefault(ability.Name, 0)
            ),
            ActiveDots = creature
                .ActiveDots.Select(d => new ActiveDot
                {
                    AbilityName = d.AbilityName,
                    Amount = d.Amount,
                    DamageType = Enum.Parse<DamageType>(d.DamageType),
                    RemainingTurns = d.RemainingTurns,
                })
                .ToList(),
            ActiveHots = creature
                .ActiveHots.Select(h => new ActiveHot
                {
                    AbilityName = h.AbilityName,
                    Amount = h.Amount,
                    RemainingTurns = h.RemainingTurns,
                })
                .ToList(),
            ActiveBuffs = creature
                .ActiveBuffs.Select(b => new ActiveBuff
                {
                    AbilityName = b.AbilityName,
                    Amount = b.Amount,
                    Attribute = Enum.Parse<AttributeName>(b.Attribute),
                    RemainingTurns = b.RemainingTurns,
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
        creature.CooldownRemainingByAbility = new Dictionary<string, int>(
            CooldownRemainingByAbility
        );
        creature.ActiveDots = ActiveDots
            .Select(d => new PersistedCombat.ActiveDot
            {
                AbilityName = d.AbilityName,
                Amount = d.Amount,
                DamageType = d.DamageType.ToString(),
                RemainingTurns = d.RemainingTurns,
            })
            .ToList();
        creature.ActiveHots = ActiveHots
            .Select(h => new PersistedCombat.ActiveHot
            {
                AbilityName = h.AbilityName,
                Amount = h.Amount,
                RemainingTurns = h.RemainingTurns,
            })
            .ToList();
        creature.ActiveBuffs = ActiveBuffs
            .Select(b => new PersistedCombat.ActiveBuff
            {
                AbilityName = b.AbilityName,
                Amount = b.Amount,
                Attribute = b.Attribute.ToString(),
                RemainingTurns = b.RemainingTurns,
                AmountType = b.AmountType.ToString(),
            })
            .ToList();

        CreatureAttributesRecalculator.Recalculate(creature, EquippedItems);

        if (!IsAlive)
        {
            creature.State = CreatureState.Dead;
        }
    }

    private float CalculateEffectiveAttribute(AttributeName attribute) =>
        StatFormulas.CalculateEffectiveAttribute(Attributes, ActiveBuffs, EquippedItems, attribute);

    public float ResistanceFor(DamageType damageType) =>
        damageType switch
        {
            DamageType.Physical => PhysicalResistance,
            DamageType.Fire => FireResistance,
            DamageType.Ice => IceResistance,
            DamageType.Lightning => LightningResistance,
            DamageType.Poison => PoisonResistance,
            DamageType.Magic => MagicResistance,
            _ => throw new ArgumentOutOfRangeException(nameof(damageType)),
        };
}
