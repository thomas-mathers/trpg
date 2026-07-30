using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Creatures;
using TRPG.Application.Inventory;
using TRPG.Data.Models;

namespace TRPG.Tests.Helpers;

internal sealed class CombatantBuilder
{
    private static readonly AttackAbility BasicAttack = AbilityDefinitions.Create().BasicAttack;
    private static readonly StatFormulas Formulas = Builders.MakeStatFormulas();

    private readonly List<Item> _items = [];
    private readonly Dictionary<WeaponType, int> _weaponProficiencies = [];
    private readonly Dictionary<ConditionType, int> _activeConditions = [];
    private Guid _worldId = Guid.NewGuid();
    private string _name = "Test Combatant";
    private bool _isPlayer;
    private CreatureType _creatureType = CreatureType.Human;
    private int _level = 1;
    private int _endurance = 10;
    private int _dexterity = 10;
    private int _strength;
    private int _stamina = 10;
    private int _defense;
    private int _intelligence;
    private float _fireResistance;
    private float _poisonResistance;
    private IReadOnlyList<Ability> _abilities = [];
    private int? _currentHp;
    private int? _currentAp;
    private int? _currentMp;
    private int? _naturalWeaponMinDamage;
    private int? _naturalWeaponMaxDamage;

    public CombatantBuilder WithWorldId(Guid worldId)
    {
        _worldId = worldId;
        return this;
    }

    public CombatantBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CombatantBuilder AsPlayer()
    {
        _isPlayer = true;
        return this;
    }

    public CombatantBuilder WithCreatureType(CreatureType creatureType)
    {
        _creatureType = creatureType;
        return this;
    }

    public CombatantBuilder WithLevel(int level)
    {
        _level = level;
        return this;
    }

    public CombatantBuilder WithEndurance(int endurance)
    {
        _endurance = endurance;
        return this;
    }

    public CombatantBuilder WithDexterity(int dexterity)
    {
        _dexterity = dexterity;
        return this;
    }

    public CombatantBuilder WithStrength(int strength)
    {
        _strength = strength;
        return this;
    }

    public CombatantBuilder WithStamina(int stamina)
    {
        _stamina = stamina;
        return this;
    }

    public CombatantBuilder WithDefense(int defense)
    {
        _defense = defense;
        return this;
    }

    public CombatantBuilder WithIntelligence(int intelligence)
    {
        _intelligence = intelligence;
        return this;
    }

    public CombatantBuilder WithFireResistance(float fireResistance)
    {
        _fireResistance = fireResistance;
        return this;
    }

    public CombatantBuilder WithPoisonResistance(float poisonResistance)
    {
        _poisonResistance = poisonResistance;
        return this;
    }

    public CombatantBuilder WithAbilities(params IReadOnlyList<Ability> abilities)
    {
        _abilities = abilities;
        return this;
    }

    public CombatantBuilder WithItem(Item item, int quantity = 1)
    {
        item.Quantity = quantity;
        item.Ownership.EquippedSlot = ItemEquipmentPolicy.GetDefaultSlot(item);
        _items.Add(item);
        return this;
    }

    public CombatantBuilder WithWeaponProficiency(WeaponType type, int proficiency)
    {
        _weaponProficiencies[type] = proficiency;
        return this;
    }

    public CombatantBuilder WithCurrentHp(int currentHp)
    {
        _currentHp = currentHp;
        return this;
    }

    public CombatantBuilder WithCurrentAp(int currentAp)
    {
        _currentAp = currentAp;
        return this;
    }

    public CombatantBuilder WithCurrentMp(int currentMp)
    {
        _currentMp = currentMp;
        return this;
    }

    public CombatantBuilder WithNaturalWeaponDamage(int minDamage, int maxDamage)
    {
        _naturalWeaponMinDamage = minDamage;
        _naturalWeaponMaxDamage = maxDamage;
        return this;
    }

    public CombatantBuilder WithCondition(ConditionType condition, int remainingTurns)
    {
        _activeConditions[condition] = remainingTurns;
        return this;
    }

    public Combatant Build()
    {
        var creature = Builders.MakeCreature(
            _worldId,
            creatureType: _creatureType,
            name: _name,
            level: _level,
            naturalWeaponMinDamage: _naturalWeaponMinDamage ?? 3,
            naturalWeaponMaxDamage: _naturalWeaponMaxDamage ?? 3
        );
        creature.BaseAttributes.Endurance = _endurance;
        creature.BaseAttributes.Dexterity = _dexterity;
        creature.BaseAttributes.Strength = _strength;
        creature.BaseAttributes.Stamina = _stamina;
        creature.BaseAttributes.Defense = _defense;
        creature.BaseAttributes.Intelligence = _intelligence;
        creature.BaseAttributes.FireResistance = _fireResistance;
        creature.BaseAttributes.PoisonResistance = _poisonResistance;
        creature.BaseAttributes.MaximumHp = Formulas.CalculateMaximumHp(creature.BaseAttributes);
        creature.BaseAttributes.MaximumAp = Formulas.CalculateMaximumAp(creature.BaseAttributes);
        creature.BaseAttributes.MaximumMp = Formulas.CalculateMaximumMp(creature.BaseAttributes);
        creature.CurrentHp = _currentHp ?? creature.BaseAttributes.MaximumHp;
        creature.CurrentAp = _currentAp ?? creature.BaseAttributes.MaximumAp;
        creature.CurrentMp = _currentMp ?? creature.BaseAttributes.MaximumMp;
        creature.ActiveConditions = _activeConditions.ToDictionary(
            kv => kv.Key.ToString(),
            kv => kv.Value
        );

        return Combatant.FromCreature(
            creature,
            [BasicAttack, .. _abilities],
            _isPlayer,
            _items,
            _weaponProficiencies
        );
    }
}
