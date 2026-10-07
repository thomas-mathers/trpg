namespace TRPG.Domain.Models;

public enum CreatureType
{
    Human,
    Elf,
    Dwarf,
    Orc,
    Halfling,
    Gnome,
    Undead,
    Demon,
    Beast,
    Construct,
    Elemental,
    Goblin,
    Wraith,
    Giant,
    Dragon,
}

public static class CreatureTypes
{
    public static readonly IReadOnlyList<CreatureType> Humanoid =
    [
        CreatureType.Human,
        CreatureType.Elf,
        CreatureType.Dwarf,
        CreatureType.Orc,
        CreatureType.Halfling,
        CreatureType.Gnome,
    ];
}

public enum CreatureCondition
{
    Awake,
    Sleeping,
    Dead,
}

public enum CreatureActivity
{
    Working,
    Studying,
    Praying,
    Eating,
}

public enum CreaturePosture
{
    Standing,
    Sitting,
    Lying,
}

public enum CreatureMovement
{
    Stationary,
    Walking,
}

public enum Gender
{
    Male,
    Female,
}

public enum Profession
{
    Knight,
    Rogue,
    Ranger,
    Mage,
    Cleric,
    Mercenary,
    Alchemist,
    Blacksmith,
    Scholar,
    Merchant,
    Politician,
    StableMaster,
    Bartender,
    Guard,
    Baker,
    Innkeeper,
    Tailor,
    Carpenter,
    Jeweler,
    Homemaker,
    Unemployed,
}

public class ActiveDot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public string DamageType { get; init; } = "";
    public GameInstant NextTickAt { get; init; }
    public GameInstant ExpiresAt { get; init; }
}

public class ActiveHot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public GameInstant NextTickAt { get; init; }
    public GameInstant ExpiresAt { get; init; }
}

public class ActiveBuff
{
    public string AbilityName { get; init; } = "";
    public float Amount { get; init; }
    public string Attribute { get; init; } = "";
    public GameInstant ExpiresAt { get; init; }
    public string AmountType { get; init; } = "";
}

public class Creature
{
    public Attributes BaseAttributes { get; set; } = null!;
    public string Biography { get; set; } = "";
    public Guid BirthLocationId { get; init; }
    public int BirthYear { get; init; }
    public CreatureType CreatureType { get; init; }
    public int CurrentAp { get; set; }
    public int CurrentHp { get; set; }
    public int CurrentMp { get; set; }
    public Gender Gender { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public GameInstant LastRegenGameTime { get; set; } = GameClock.Epoch;
    public int Level { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; init; } = "";
    public Profession? Profession { get; set; }
    public Guid? PlayerCorpseOwnerId { get; init; }
    public Guid? PreviousLocationId { get; set; }
    public GameInstant? RestedUntilGameTime { get; set; }
    public Guid? SpawnerId { get; set; }
    public CreatureCondition Condition { get; set; }
    public CreatureActivity? Activity { get; set; }
    public CreaturePosture Posture { get; set; }
    public CreatureMovement Movement { get; set; }
    public bool IsEngaged { get; set; }
    public bool IsSneaking { get; set; }
    public bool IsAlerted { get; set; }
    public bool IsRestrained { get; set; }
    public Guid WorldId { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Angle { get; set; }
    public double? StandingX { get; set; }
    public double? StandingY { get; set; }
    public double? StandingAngle { get; set; }
    public double? EntryX { get; set; }
    public double? EntryY { get; set; }
    public GameInstant? EnteredAt { get; set; }
    public double? ExitX { get; set; }
    public double? ExitY { get; set; }
    public GameInstant? DepartedAt { get; set; }

    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Intelligence { get; set; }
    public int Endurance { get; set; }
    public int Stamina { get; set; }
    public int Mana { get; set; }
    public int Defense { get; set; }
    public int MaximumHp { get; set; }
    public int MaximumAp { get; set; }
    public int MaximumMp { get; set; }
    public int CarryingCapacity { get; set; }
    public float MovementSpeed { get; set; }
    public float PhysicalResistance { get; set; }
    public float FireResistance { get; set; }
    public float IceResistance { get; set; }
    public float LightningResistance { get; set; }
    public float PoisonResistance { get; set; }
    public float MagicResistance { get; set; }
    public int NaturalWeaponMinDamage { get; set; }
    public int NaturalWeaponMaxDamage { get; set; }
    public Dictionary<string, GameInstant> ActiveConditions { get; set; } = [];
    public Dictionary<string, GameInstant> CooldownReadyAtByAbility { get; set; } = [];
    public List<ActiveDot> ActiveDots { get; set; } = [];
    public List<ActiveHot> ActiveHots { get; set; } = [];
    public List<ActiveBuff> ActiveBuffs { get; set; } = [];

    public bool HasActiveDots => ActiveDots.Count > 0;

    public bool HasActiveEffects =>
        ActiveConditions.Count > 0
        || CooldownReadyAtByAbility.Count > 0
        || ActiveDots.Count > 0
        || ActiveHots.Count > 0
        || ActiveBuffs.Count > 0;

    public void Die()
    {
        Condition = CreatureCondition.Dead;
        Activity = null;
        Movement = CreatureMovement.Stationary;
        IsAlerted = false;
        IsSneaking = false;
    }
}
