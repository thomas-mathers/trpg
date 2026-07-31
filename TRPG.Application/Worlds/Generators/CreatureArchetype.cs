using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public record StatAffinities(
    int Strength,
    int Dexterity,
    int Endurance,
    int Stamina,
    int Mana,
    int Intelligence,
    float GoldMultiplier
);

public abstract record StartingGearSpec;

public sealed record WeaponSpec(WeaponType WeaponType, EquipmentSlot? SlotOverride = null)
    : StartingGearSpec;

public sealed record ShieldSpec : StartingGearSpec;

public sealed record AmmoSpec(AmmoType AmmoType, int Quantity) : StartingGearSpec;

public sealed record ConsumableSpec(int Quantity) : StartingGearSpec;

public sealed record NaturalWeaponDamageRange(
    int MinDamageLow,
    int MinDamageHigh,
    int MaxDamageLow,
    int MaxDamageHigh
);

public sealed class CreatureArchetype
{
    private static readonly NaturalWeaponDamageRange SmallNaturalWeapon = new(1, 5, 3, 14);
    private static readonly NaturalWeaponDamageRange HumanoidNaturalWeapon = new(1, 6, 4, 20);
    private static readonly NaturalWeaponDamageRange LargeNaturalWeapon = new(3, 15, 10, 45);

    public Profession? Profession { get; }
    public CreatureType? CreatureType { get; }
    public StatAffinities StatAffinities { get; }
    public IReadOnlyDictionary<Skill, int> SkillAffinities { get; }
    public IReadOnlyList<StartingGearSpec> StartingGear { get; }
    public ArmorClass? ArmorClass { get; }
    public bool HasAccessories { get; }
    public bool HasPotions { get; }
    public NaturalWeaponDamageRange NaturalWeaponDamage { get; }
    public string? Biography { get; }

    private CreatureArchetype(
        StatAffinities statAffinities,
        IReadOnlyDictionary<Skill, int> skillAffinities,
        Profession? profession = null,
        CreatureType? creatureType = null,
        IReadOnlyList<StartingGearSpec>? startingGear = null,
        ArmorClass? armorClass = null,
        bool hasAccessories = false,
        bool hasPotions = false,
        NaturalWeaponDamageRange? naturalWeaponDamage = null,
        string? biography = null
    )
    {
        Profession = profession;
        CreatureType = creatureType;
        StatAffinities = statAffinities;
        SkillAffinities = skillAffinities;
        StartingGear = startingGear ?? [];
        ArmorClass = armorClass;
        HasAccessories = hasAccessories;
        HasPotions = hasPotions;
        NaturalWeaponDamage = naturalWeaponDamage ?? HumanoidNaturalWeapon;
        Biography = biography;
    }

    private static readonly Dictionary<Skill, int> CivilianSkillAffinities = new()
    {
        [Skill.General] = 1,
    };

    private static readonly Dictionary<Skill, int> SoldierSkillAffinities = new()
    {
        [Skill.Melee] = 3,
        [Skill.Blocking] = 2,
        [Skill.General] = 1,
    };

    public static readonly CreatureArchetype Beast = new(
        statAffinities: new StatAffinities(
            Strength: 3,
            Dexterity: 2,
            Endurance: 2,
            Stamina: 2,
            Mana: 0,
            Intelligence: 0,
            GoldMultiplier: 0.2f
        ),
        skillAffinities: new Dictionary<Skill, int> { [Skill.Melee] = 2, [Skill.General] = 1 },
        creatureType: Data.Models.CreatureType.Beast,
        biography: "A feral creature of claw and hunger, hostile to intruders."
    );

    public static readonly CreatureArchetype Undead = new(
        statAffinities: new StatAffinities(
            Strength: 2,
            Dexterity: 1,
            Endurance: 2,
            Stamina: 1,
            Mana: 0,
            Intelligence: 0,
            GoldMultiplier: 0.6f
        ),
        skillAffinities: new Dictionary<Skill, int>
        {
            [Skill.Melee] = 2,
            [Skill.Blocking] = 1,
            [Skill.General] = 1,
        },
        creatureType: Data.Models.CreatureType.Undead,
        startingGear: [new WeaponSpec(WeaponType.Sword)],
        armorClass: Data.Models.ArmorClass.Mail,
        biography: "A restless corpse animated by something that is not life."
    );

    public static readonly CreatureArchetype Construct = new(
        statAffinities: new StatAffinities(
            Strength: 3,
            Dexterity: 0,
            Endurance: 3,
            Stamina: 1,
            Mana: 0,
            Intelligence: 0,
            GoldMultiplier: 0.4f
        ),
        skillAffinities: new Dictionary<Skill, int> { [Skill.Blocking] = 2, [Skill.General] = 1 },
        creatureType: Data.Models.CreatureType.Construct,
        naturalWeaponDamage: LargeNaturalWeapon,
        biography: "An artificial thing still obeying an order given long ago."
    );

    public static readonly CreatureArchetype Demon = new(
        statAffinities: new StatAffinities(
            Strength: 3,
            Dexterity: 1,
            Endurance: 2,
            Stamina: 1,
            Mana: 2,
            Intelligence: 1,
            GoldMultiplier: 1.0f
        ),
        skillAffinities: new Dictionary<Skill, int>
        {
            [Skill.Destruction] = 2,
            [Skill.Melee] = 2,
            [Skill.General] = 1,
        },
        creatureType: Data.Models.CreatureType.Demon,
        startingGear: [new WeaponSpec(WeaponType.Sword)],
        hasPotions: true,
        biography: "A malevolent entity from somewhere that is not this world."
    );

    public static readonly CreatureArchetype Elemental = new(
        statAffinities: new StatAffinities(
            Strength: 0,
            Dexterity: 1,
            Endurance: 1,
            Stamina: 1,
            Mana: 4,
            Intelligence: 2,
            GoldMultiplier: 0.2f
        ),
        skillAffinities: new Dictionary<Skill, int>
        {
            [Skill.Destruction] = 3,
            [Skill.General] = 1,
        },
        creatureType: Data.Models.CreatureType.Elemental,
        biography: "Raw elemental force bound loosely into a walking shape."
    );

    public static readonly CreatureArchetype Goblin = new(
        statAffinities: new StatAffinities(
            Strength: 1,
            Dexterity: 3,
            Endurance: 1,
            Stamina: 2,
            Mana: 0,
            Intelligence: 1,
            GoldMultiplier: 0.4f
        ),
        skillAffinities: new Dictionary<Skill, int>
        {
            [Skill.Archery] = 2,
            [Skill.Sneak] = 1,
            [Skill.General] = 1,
        },
        creatureType: Data.Models.CreatureType.Goblin,
        startingGear:
        [
            new WeaponSpec(WeaponType.Bow),
            new AmmoSpec(AmmoType.Arrow, 20),
            new WeaponSpec(WeaponType.Dagger),
        ],
        armorClass: Data.Models.ArmorClass.Leather,
        hasPotions: true,
        naturalWeaponDamage: SmallNaturalWeapon,
        biography: "A small, vicious scavenger that hunts in packs and covets anything shiny."
    );

    public static readonly CreatureArchetype Wraith = new(
        statAffinities: new StatAffinities(
            Strength: 0,
            Dexterity: 2,
            Endurance: 1,
            Stamina: 1,
            Mana: 3,
            Intelligence: 2,
            GoldMultiplier: 0.3f
        ),
        skillAffinities: new Dictionary<Skill, int>
        {
            [Skill.Destruction] = 1,
            [Skill.Illusion] = 1,
            [Skill.General] = 1,
        },
        creatureType: Data.Models.CreatureType.Wraith,
        biography: "A hateful spirit bound by unholy devotion to something long dead."
    );

    public static readonly CreatureArchetype Giant = new(
        statAffinities: new StatAffinities(
            Strength: 4,
            Dexterity: 0,
            Endurance: 2,
            Stamina: 1,
            Mana: 0,
            Intelligence: 0,
            GoldMultiplier: 0.5f
        ),
        skillAffinities: new Dictionary<Skill, int> { [Skill.Melee] = 3, [Skill.General] = 1 },
        creatureType: Data.Models.CreatureType.Giant,
        startingGear: [new WeaponSpec(WeaponType.Mace)],
        naturalWeaponDamage: LargeNaturalWeapon,
        biography: "A towering brute whose footsteps announce it long before it is seen."
    );

    public static readonly CreatureArchetype Dragon = new(
        statAffinities: new StatAffinities(
            Strength: 3,
            Dexterity: 1,
            Endurance: 2,
            Stamina: 1,
            Mana: 2,
            Intelligence: 1,
            GoldMultiplier: 2.0f
        ),
        skillAffinities: new Dictionary<Skill, int>
        {
            [Skill.Destruction] = 2,
            [Skill.Melee] = 2,
            [Skill.General] = 1,
        },
        creatureType: Data.Models.CreatureType.Dragon,
        naturalWeaponDamage: LargeNaturalWeapon,
        biography: "An ancient winged predator of scale and flame, jealous of its hoard."
    );

    private static readonly Dictionary<Profession, CreatureArchetype> ByProfession = new()
    {
        [Data.Models.Profession.Knight] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 3,
                Dexterity: 0,
                Endurance: 2,
                Stamina: 2,
                Mana: 0,
                Intelligence: 0,
                GoldMultiplier: 0.8f
            ),
            skillAffinities: SoldierSkillAffinities,
            profession: Data.Models.Profession.Knight,
            startingGear: [new WeaponSpec(WeaponType.Sword), new ShieldSpec()],
            armorClass: Data.Models.ArmorClass.Plate,
            hasAccessories: true
        ),
        [Data.Models.Profession.Rogue] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 1,
                Dexterity: 4,
                Endurance: 1,
                Stamina: 2,
                Mana: 0,
                Intelligence: 2,
                GoldMultiplier: 1.2f
            ),
            skillAffinities: new Dictionary<Skill, int>
            {
                [Skill.Sneak] = 3,
                [Skill.Melee] = 2,
                [Skill.General] = 1,
            },
            profession: Data.Models.Profession.Rogue,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Leather,
            hasAccessories: true
        ),
        [Data.Models.Profession.Ranger] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 2,
                Dexterity: 3,
                Endurance: 2,
                Stamina: 2,
                Mana: 0,
                Intelligence: 2,
                GoldMultiplier: 0.9f
            ),
            skillAffinities: new Dictionary<Skill, int>
            {
                [Skill.Archery] = 2,
                [Skill.General] = 1,
            },
            profession: Data.Models.Profession.Ranger,
            startingGear: [new WeaponSpec(WeaponType.Bow), new AmmoSpec(AmmoType.Arrow, 20)],
            armorClass: Data.Models.ArmorClass.Leather,
            hasAccessories: true
        ),
        [Data.Models.Profession.Mage] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 0,
                Endurance: 1,
                Stamina: 0,
                Mana: 4,
                Intelligence: 5,
                GoldMultiplier: 1.5f
            ),
            skillAffinities: new Dictionary<Skill, int>
            {
                [Skill.Destruction] = 1,
                [Skill.Illusion] = 1,
                [Skill.General] = 1,
            },
            profession: Data.Models.Profession.Mage,
            startingGear: [new WeaponSpec(WeaponType.Staff), new ConsumableSpec(3)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Cleric] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 0,
                Endurance: 1,
                Stamina: 1,
                Mana: 3,
                Intelligence: 3,
                GoldMultiplier: 1.0f
            ),
            skillAffinities: new Dictionary<Skill, int>
            {
                [Skill.Restoration] = 2,
                [Skill.Alteration] = 2,
                [Skill.General] = 1,
            },
            profession: Data.Models.Profession.Cleric,
            startingGear: [new WeaponSpec(WeaponType.Mace), new ShieldSpec()],
            armorClass: Data.Models.ArmorClass.Plate,
            hasAccessories: true
        ),
        [Data.Models.Profession.Mercenary] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 3,
                Dexterity: 1,
                Endurance: 1,
                Stamina: 3,
                Mana: 0,
                Intelligence: 0,
                GoldMultiplier: 1.1f
            ),
            skillAffinities: SoldierSkillAffinities,
            profession: Data.Models.Profession.Mercenary,
            startingGear: [new WeaponSpec(WeaponType.Sword), new ShieldSpec()],
            armorClass: Data.Models.ArmorClass.Mail,
            hasAccessories: true
        ),
        [Data.Models.Profession.Alchemist] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 2,
                Endurance: 1,
                Stamina: 0,
                Mana: 3,
                Intelligence: 4,
                GoldMultiplier: 2.0f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Alchemist,
            startingGear: [new WeaponSpec(WeaponType.Wand), new ConsumableSpec(5)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Blacksmith] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 4,
                Dexterity: 1,
                Endurance: 3,
                Stamina: 1,
                Mana: 0,
                Intelligence: 0,
                GoldMultiplier: 1.5f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Blacksmith,
            startingGear: [new WeaponSpec(WeaponType.Axe)],
            armorClass: Data.Models.ArmorClass.Plate,
            hasAccessories: true
        ),
        [Data.Models.Profession.Scholar] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 1,
                Endurance: 1,
                Stamina: 0,
                Mana: 1,
                Intelligence: 7,
                GoldMultiplier: 2.0f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Scholar,
            startingGear: [new WeaponSpec(WeaponType.Staff)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Merchant] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 3,
                Endurance: 1,
                Stamina: 1,
                Mana: 0,
                Intelligence: 5,
                GoldMultiplier: 3.0f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Merchant,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Leather,
            hasAccessories: true
        ),
        [Data.Models.Profession.Politician] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 1,
                Endurance: 0,
                Stamina: 0,
                Mana: 1,
                Intelligence: 8,
                GoldMultiplier: 4.0f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Politician,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.StableMaster] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 1,
                Dexterity: 3,
                Endurance: 3,
                Stamina: 2,
                Mana: 0,
                Intelligence: 1,
                GoldMultiplier: 1.0f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.StableMaster,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Leather,
            hasAccessories: true
        ),
        [Data.Models.Profession.Bartender] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 3,
                Endurance: 1,
                Stamina: 2,
                Mana: 0,
                Intelligence: 4,
                GoldMultiplier: 1.2f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Bartender,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Guard] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 2,
                Dexterity: 1,
                Endurance: 3,
                Stamina: 1,
                Mana: 0,
                Intelligence: 0,
                GoldMultiplier: 0.7f
            ),
            skillAffinities: SoldierSkillAffinities,
            profession: Data.Models.Profession.Guard,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Mail,
            hasAccessories: true
        ),
        [Data.Models.Profession.Baker] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 1,
                Dexterity: 2,
                Endurance: 2,
                Stamina: 2,
                Mana: 0,
                Intelligence: 2,
                GoldMultiplier: 1.3f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Baker,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Innkeeper] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 2,
                Endurance: 1,
                Stamina: 2,
                Mana: 0,
                Intelligence: 3,
                GoldMultiplier: 1.4f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Innkeeper,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Tailor] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 3,
                Endurance: 1,
                Stamina: 1,
                Mana: 0,
                Intelligence: 3,
                GoldMultiplier: 1.5f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Tailor,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Carpenter] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 2,
                Dexterity: 2,
                Endurance: 2,
                Stamina: 2,
                Mana: 0,
                Intelligence: 1,
                GoldMultiplier: 1.2f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Carpenter,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Leather,
            hasAccessories: true
        ),
        [Data.Models.Profession.Jeweler] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 3,
                Endurance: 0,
                Stamina: 1,
                Mana: 0,
                Intelligence: 3,
                GoldMultiplier: 2.5f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Jeweler,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Homemaker] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 1,
                Endurance: 2,
                Stamina: 2,
                Mana: 0,
                Intelligence: 1,
                GoldMultiplier: 0.5f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Homemaker,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
        [Data.Models.Profession.Unemployed] = new CreatureArchetype(
            statAffinities: new StatAffinities(
                Strength: 0,
                Dexterity: 1,
                Endurance: 1,
                Stamina: 1,
                Mana: 0,
                Intelligence: 1,
                GoldMultiplier: 0.3f
            ),
            skillAffinities: CivilianSkillAffinities,
            profession: Data.Models.Profession.Unemployed,
            startingGear: [new WeaponSpec(WeaponType.Dagger)],
            armorClass: Data.Models.ArmorClass.Cloth,
            hasAccessories: true
        ),
    };

    public static CreatureArchetype For(Profession profession) => ByProfession[profession];
}
