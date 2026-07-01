namespace TRPG.Models;

internal enum BuildingType {
    ArcaneShop,
    Apothecary,
    Bakery,
    Blacksmith,
    Castle,
    Cave,
    Crypt,
    GeneralGoods,
    GuildHall,
    House,
    Jail,
    Library,
    Mine,
    Ruins,
    Stable,
    Tavern,
    Temple,
    Tower
}

internal class Building {
    public Rectangle Boundary { get; set; } = null!;
    public BuildingType BuildingType { get; init; }
    public Guid RegionId { get; init; }
    public string Description { get; init; } = "";
    public Guid? FactionId { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}