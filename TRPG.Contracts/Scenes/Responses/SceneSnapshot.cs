using System.ComponentModel;
using TRPG.Contracts.Worlds.Requests;

namespace TRPG.Contracts.Scenes.Responses;

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

    [Description("Stable Master")]
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

public enum DistrictType
{
    Residential,
    Scientific,

    [Description("City Center")]
    CityCenter,
    Governmental,

    [Description("Holy Site")]
    HolySite,
    Encampment,
}

public enum BuildingType
{
    [Description("Arcane Shop")]
    ArcaneShop,
    Apothecary,
    Bakery,
    Barracks,
    Blacksmith,
    Carpenter,
    Castle,
    Cave,
    Crypt,

    [Description("General Goods")]
    GeneralGoods,

    [Description("Guild Hall")]
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

public record SceneSnapshot(
    string StateName,
    string? CityName,
    string? DistrictName,
    string? BuildingName,
    string? RoomName,
    int Year,
    string MonthName,
    int Day,
    string WeekdayName,
    int Hour,
    CreatureStatusSnapshot PlayerStatus,
    IReadOnlyCollection<CreatureStatusSnapshot> NearbyCreatures,
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyBuildings,
    IReadOnlyCollection<NearbyBuildingSnapshot> NearbyDungeons,
    IReadOnlyCollection<NearbyPropSnapshot> NearbyProps,
    IReadOnlyCollection<NearbyExitSnapshot> Exits
);

public record CreatureStatusSnapshot(
    Guid Id,
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int Age,
    CreatureState? State,
    int Gold,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    int ExperienceCurrent,
    int ExperienceToNextLevel,
    IReadOnlyCollection<string>? FactionNames,
    int? Reputation,
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Mana,
    int Defense,
    float MovementSpeed,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance
);

public record NearbyBuildingSnapshot(
    Guid Id,
    string Name,
    BuildingType Type,
    string TypeDescription
);

public record NearbyPropSnapshot(Guid Id, string Name, string Type);

public record NearbyExitSnapshot(string Description, string DestinationRoomName);
