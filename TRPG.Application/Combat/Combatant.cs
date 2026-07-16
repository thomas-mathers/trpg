using TRPG.Application.Abilities;
using TRPG.Application.Creatures;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class ActiveBuff
{
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public int RemainingTurns { get; set; }
    public AmountType AmountType { get; init; }
}

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

public class Combatant
{
    public required Guid CreatureId { get; init; }
    public required string Name { get; init; }
    public required bool IsPlayer { get; init; }
    public required int Level { get; init; }
    public required Attributes Attributes { get; init; }
    public required IReadOnlyList<Ability> Abilities { get; init; }
    public required int Gold { get; init; }
    public int CurrentHp { get; set; }
    public int CurrentAp { get; set; }
    public int CurrentMp { get; set; }
    public IReadOnlyList<Item> Inventory { get; init; } = [];
    public Dictionary<WeaponType, int> WeaponProficiencies { get; init; } = [];
    public Dictionary<WeaponType, int> WeaponSwingCounts { get; init; } = [];
    public Dictionary<ConditionType, int> ActiveConditions { get; init; } = [];
    public List<ActiveDot> ActiveDots { get; init; } = [];
    public List<ActiveHot> ActiveHots { get; init; } = [];
    public List<ActiveBuff> ActiveBuffs { get; init; } = [];
    public Dictionary<string, int> CooldownRemainingByAbility { get; init; } = [];
    public bool IsAlive => CurrentHp > 0;
    public int MaximumHp => (int)CalculateEffectiveAttribute(AttributeName.MaximumHp);
    public int MaximumAp => (int)CalculateEffectiveAttribute(AttributeName.MaximumAp);
    public int MaximumMp => (int)CalculateEffectiveAttribute(AttributeName.MaximumMp);
    public WeaponItem? Weapon => Inventory.OfType<WeaponItem>().SingleOrDefault();
    public int? WeaponProficiency => Weapon != null ? WeaponProficiencies[Weapon.Type] : null;
    public ShieldItem? Shield => Inventory.OfType<ShieldItem>().SingleOrDefault();
    public float BlockChance => Shield?.BlockChance ?? 0f;

    public static Combatant FromCreature(
        Creature creature,
        IReadOnlyList<Ability> abilities,
        AttackAbility basicAttack,
        bool isPlayer,
        IReadOnlyList<Item> inventory,
        Dictionary<WeaponType, int> weaponProficiencies
    )
    {
        var allAbilities = new[] { basicAttack }.Concat(abilities).ToArray();
        var startingAttributes = StatFormulas.CalculateEffectiveAttributes(
            creature.Attributes,
            [],
            inventory
        );

        var combatant = new Combatant
        {
            CreatureId = creature.Id,
            Name = creature.Name,
            IsPlayer = isPlayer,
            Level = creature.Level,
            Attributes = creature.Attributes,
            Abilities = allAbilities,
            Gold = creature.Gold,
            CurrentHp = Math.Clamp(creature.CurrentHp, 1, startingAttributes.MaximumHp),
            CurrentAp = Math.Min(creature.CurrentAp, startingAttributes.MaximumAp),
            CurrentMp = Math.Min(creature.CurrentMp, startingAttributes.MaximumMp),
            Inventory = inventory,
            WeaponProficiencies = Enum.GetValues<WeaponType>()
                .ToDictionary(type => type, type => weaponProficiencies.GetValueOrDefault(type)),
            ActiveConditions = Enum.GetValues<ConditionType>()
                .ToDictionary(condition => condition, _ => 0),
            CooldownRemainingByAbility = allAbilities.ToDictionary(ability => ability.Name, _ => 0),
        };

        return combatant;
    }

    public float CalculateEffectiveAttribute(AttributeName attribute) =>
        StatFormulas.CalculateEffectiveAttribute(Attributes, ActiveBuffs, Inventory, attribute);

    public Attributes CalculateEffectiveAttributes() =>
        StatFormulas.CalculateEffectiveAttributes(Attributes, ActiveBuffs, Inventory);
}
