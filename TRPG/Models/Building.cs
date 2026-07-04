namespace TRPG.Models;

internal enum BuildingType {
    ArcaneShop,
    Apothecary,
    Bakery,
    Barracks,
    Blacksmith,
    Castle,
    Cave,
    Crypt,
    GeneralGoods,
    GuildHall,
    House,
    Inn,
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
    public BuildingType BuildingType { get; init; }
    public Guid StateId { get; init; }
    public Guid? CityId { get; init; }
    public Guid? DistrictId { get; init; }
    public string Description { get; init; } = "";
    public Guid? FactionId { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}