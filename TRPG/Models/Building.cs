namespace TRPG.Models;

internal enum BuildingType {
    ArcaneShop,
    Apothecary,
    Bakery,
    Blacksmith,
    Castle,
    GeneralGoods,
    GuildHall,
    House,
    Jail,
    Library,
    Stable,
    Tavern,
    Temple
}

internal class Building {
    public Rectangle Boundary { get; set; } = null!;
    public BuildingType BuildingType { get; init; }
    public Guid CityId { get; init; }
    public string Description { get; init; } = "";
    public Guid? FactionId { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}