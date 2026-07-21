using TRPG.Application.Abilities;
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

public record UsableItem(Guid ItemId, string Name, ResourceType Resource, int Amount)
{
    public static IReadOnlyList<UsableItem> FromInventory(
        IReadOnlyCollection<InventoryItem> inventoryItems
    ) =>
        inventoryItems
            .Where(i => i.Item is ConsumableItem)
            .Select(i => new UsableItem(
                i.ItemId,
                i.Item.Name,
                ((ConsumableItem)i.Item).Resource,
                ((ConsumableItem)i.Item).Amount
            ))
            .ToArray();
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
    public IReadOnlyList<UsableItem> UsableItems { get; init; } = [];
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
        BuffAbility blockStance,
        bool isPlayer,
        IReadOnlyList<Item> inventory,
        IReadOnlyDictionary<WeaponType, int> weaponProficiencies,
        IReadOnlyList<UsableItem> usableItems
    )
    {
        var allAbilities = new Ability[] { basicAttack, blockStance }.Concat(abilities).ToArray();
        var startingAttributes = StatFormulas.CalculateEffectiveAttributes(
            creature.BaseAttributes,
            [],
            inventory
        );

        var combatant = new Combatant
        {
            CreatureId = creature.Id,
            Name = creature.Name,
            IsPlayer = isPlayer,
            Level = creature.Level,
            Attributes = creature.BaseAttributes,
            Abilities = allAbilities,
            Gold = creature.Gold,
            CurrentHp = Math.Clamp(creature.CurrentHp, 0, startingAttributes.MaximumHp),
            CurrentAp = Math.Min(creature.CurrentAp, startingAttributes.MaximumAp),
            CurrentMp = Math.Min(creature.CurrentMp, startingAttributes.MaximumMp),
            Inventory = inventory,
            UsableItems = usableItems,
            WeaponProficiencies = Enum.GetValues<WeaponType>()
                .ToDictionary(type => type, type => weaponProficiencies.GetValueOrDefault(type)),
            ActiveConditions = Enum.GetValues<ConditionType>()
                .ToDictionary(
                    condition => condition,
                    condition =>
                        creature.ActiveConditions.GetValueOrDefault(condition.ToString(), 0)
                ),
            CooldownRemainingByAbility = allAbilities.ToDictionary(
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

        CreatureAttributesRecalculator.Recalculate(creature, Inventory);

        if (!IsAlive)
        {
            creature.State = CreatureState.Dead;
        }
    }

    public float CalculateEffectiveAttribute(AttributeName attribute) =>
        StatFormulas.CalculateEffectiveAttribute(Attributes, ActiveBuffs, Inventory, attribute);
}
