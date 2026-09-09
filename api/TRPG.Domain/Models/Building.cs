namespace TRPG.Domain.Models;

public enum BuildingType
{
    ArcaneShop,
    Apothecary,
    Bakery,
    Barracks,
    Blacksmith,
    Carpenter,
    Castle,
    Cave,
    Crypt,
    GeneralGoods,
    GuildHall,
    House,
    Inn,
    Jail,
    Jeweler,
    Library,
    Mine,
    Ruins,
    Stable,
    Tailor,
    Tavern,
    Temple,
    Tower,
}

public static class BuildingTypes
{
    public static readonly IReadOnlyCollection<BuildingType> Dungeon =
    [
        BuildingType.Cave,
        BuildingType.Crypt,
        BuildingType.Mine,
        BuildingType.Ruins,
        BuildingType.Tower,
    ];
}

public class Building
{
    public BuildingType BuildingType { get; init; }
    public string Description { get; init; } = "";
    public Guid ExteriorLocationId { get; init; }
    public Guid? FactionId { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";

    // One line saying what this place is and why it is like this, so its rooms read as one place
    // rather than eleven independently flavoured ones. Written on first entry, not at world
    // generation: most dungeons in a world are never walked into.
    public string? Premise { get; set; }
    public Guid WorldId { get; init; }
}
