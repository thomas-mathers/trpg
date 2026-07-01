namespace TRPG.Models;

internal enum Profession {
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
    Guard
}

internal class Person {
    public Dictionary<ConditionType, int> ActiveConditions { get; set; } = [];
    public List<ActiveModifier> ActiveModifiers { get; set; } = [];
    public Attributes Attributes { get; set; } = null!;
    public string Biography { get; set; } = "";
    public Guid BirthRegionId { get; init; }
    public int BirthYear { get; init; }
    public int Experience { get; set; }
    public int Gold { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Level { get; set; }
    public Location Location { get; set; } = null!;
    public string Name { get; init; } = "";
    public Profession Profession { get; init; }
    public Guid RaceId { get; init; }
    public Guid WorldId { get; init; }
}