namespace TRPG.Models;

internal enum CreatureType
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

internal static class CreatureTypes
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

internal enum CreatureState
{
    Sleeping,
    Idle,
    Busy,
    Studying,
    Praying,
    Training,
    Sitting,
}

internal enum Gender
{
    Male,
    Female,
}

internal enum Profession
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

internal class Creature
{
    public Dictionary<ConditionType, int> ActiveConditions { get; set; } = [];
    public List<ActiveModifier> ActiveModifiers { get; set; } = [];
    public Attributes Attributes { get; set; } = null!;
    public string Biography { get; set; } = "";
    public Guid BirthStateId { get; init; }
    public int BirthYear { get; init; }
    public Guid? CityId { get; set; }
    public CreatureType CreatureType { get; init; }
    public Guid? DistrictId { get; set; }
    public int Experience { get; set; }
    public Gender Gender { get; init; }
    public int Gold { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Level { get; set; }
    public string Name { get; init; } = "";
    public Profession? Profession { get; set; }
    public Guid? RoomId { get; set; }
    public CreatureState State { get; set; }
    public Guid StateId { get; set; }
    public Guid WorldId { get; init; }
}
