namespace TRPG.Data.Models;

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

public enum CreatureState
{
    Sleeping,
    Idle,
    Busy,
    Studying,
    Praying,
    Training,
    Sitting,
    Dead,
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

public class Creature
{
    public Attributes Attributes { get; set; } = null!;
    public string Biography { get; set; } = "";
    public Guid BirthStateId { get; init; }
    public int BirthYear { get; init; }
    public Guid? CityId { get; set; }
    public CreatureType CreatureType { get; init; }
    public int CurrentAp { get; set; }
    public int CurrentHp { get; set; }
    public int CurrentMp { get; set; }
    public Guid? DistrictId { get; set; }
    public int Experience { get; set; }
    public Gender Gender { get; init; }
    public int Gold { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public TimeSpan LastRegenPlaytime { get; set; }
    public int Level { get; set; }
    public string Name { get; init; } = "";
    public Profession? Profession { get; set; }
    public Guid? RoomId { get; set; }
    public CreatureState State { get; set; }
    public Guid StateId { get; set; }
    public Guid WorldId { get; init; }
}
