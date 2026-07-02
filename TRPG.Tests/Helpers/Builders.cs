using TRPG.Models;

namespace TRPG.Tests.Helpers;

internal static class Builders {
    public static Person MakePerson(Guid? worldId = null, Guid? raceId = null,
        Profession profession = Profession.Knight,
        Guid? birthRegionId = null,
        Guid? regionId = null) {
        return new Person {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = "Test Person",
            RaceId = raceId ?? Guid.NewGuid(),
            BirthRegionId = birthRegionId ?? Guid.NewGuid(),
            BirthYear = 1000,
            Profession = profession,
            RegionId = regionId ?? Guid.NewGuid(),
            Level = 1,
            Attributes = new Attributes {
                Strength = 10,
                Defense = 5,
                Dexterity = 8,
                Endurance = 7,
                Stamina = 6,
                Mana = 4,
                Intelligence = 9,
                HpPercent = 1.0f,
                ApPercent = 1.0f,
                MpPercent = 1.0f
            }
        };
    }

    public static Item MakeItem(Guid? worldId = null) {
        return new Item {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test item",
            Weight = 1,
            GoldValue = 10
        };
    }

    public static WeaponItem MakeWeaponItem(Guid? worldId = null, WeaponType type = WeaponType.Sword) {
        return new WeaponItem {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test weapon",
            Weight = 8,
            GoldValue = 50,
            Type = type,
            MinDamage = 5,
            MaxDamage = 15,
            Range = 1,
            AttackSpeed = 7,
            DurabilityMax = 100,
            DurabilityCurrent = 100
        };
    }

    public static ArmorItem MakeArmorItem(Guid? worldId = null, ArmorType type = ArmorType.Chest) {
        return new ArmorItem {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test armor",
            Weight = 15,
            GoldValue = 40,
            Type = type,
            Defense = 10,
            DurabilityMax = 100,
            DurabilityCurrent = 100
        };
    }

    public static ShieldItem MakeShieldItem(Guid? worldId = null) {
        return new ShieldItem {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test shield",
            Weight = 8,
            GoldValue = 30,
            Defense = 8,
            BlockChance = 25,
            DurabilityMax = 100,
            DurabilityCurrent = 100
        };
    }

    public static ConsumableItem MakeConsumableItem(Guid? worldId = null) {
        return new ConsumableItem {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test consumable",
            Weight = 1,
            GoldValue = 10,
            Attribute = AttributeName.Hp,
            Amount = 50,
            Duration = 0
        };
    }

    public static AmmunitionItem MakeAmmunitionItem(Guid? worldId = null, AmmoType type = AmmoType.Arrow) {
        return new AmmunitionItem {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test ammo",
            Weight = 2,
            GoldValue = 5,
            Type = type
        };
    }

    public static AccessoryItem MakeAccessoryItem(Guid? worldId = null, AccessoryType type = AccessoryType.Ring) {
        return new AccessoryItem {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test accessory",
            Weight = 1,
            GoldValue = 20,
            Type = type
        };
    }


    public static Quest MakeQuest(Guid giverId, Guid? worldId = null) {
        return new Quest {
            WorldId = worldId ?? Guid.NewGuid(),
            GiverId = giverId,
            Name = $"Quest-{Guid.NewGuid():N}",
            Description = "A test quest",
            GoldReward = 100,
            ExperienceReward = 50
        };
    }

    public static Race MakeRace(Guid? worldId = null) {
        return new Race
            { WorldId = worldId ?? Guid.NewGuid(), Name = $"Race-{Guid.NewGuid():N}", Description = "A test race" };
    }


    public static Faction MakeFaction(Guid? worldId = null) {
        return new Faction {
            WorldId = worldId ?? Guid.NewGuid(), Name = $"Faction-{Guid.NewGuid():N}", Description = "A test faction"
        };
    }

    public static World MakeWorld() {
        return new World {
            Name = $"World-{Guid.NewGuid():N}", Description = "A test world",
            Boundary = new Rectangle(0, 0, 10000, 10000)
        };
    }

    public static Country MakeCountry(Guid worldId) {
        return new Country {
            WorldId = worldId,
            Name = $"Country-{Guid.NewGuid():N}",
            Description = "A test country",
            Boundary = new Polygon
                { Points = [new Point(0, 0), new Point(3000, 0), new Point(3000, 3000), new Point(0, 3000)] }
        };
    }

    public static Region MakeRegion(Guid countryId) {
        return new Region {
            CountryId = countryId,
            Name = $"Region-{Guid.NewGuid():N}",
            Description = "A test region",
            Width = 100,
            Height = 100,
            Boundary = new Polygon
                { Points = [new Point(0, 0), new Point(100, 0), new Point(100, 100), new Point(0, 100)] }
        };
    }

    public static Room MakeRoom(Guid buildingId) {
        return new Room {
            BuildingId = buildingId,
            Name = $"Room-{Guid.NewGuid():N}",
            Description = "A test room",
            FloorNumber = 0
        };
    }

    public static Building MakeBuilding(Guid regionId) {
        return new Building {
            RegionId = regionId,
            Name = $"Building-{Guid.NewGuid():N}",
            Description = "A test building",
            BuildingType = BuildingType.House
        };
    }

    public static Job MakeJob(Guid personId, int priority = 1) {
        return new Job {
            PersonId = personId,
            Action = JobAction.Idle,
            StartHour = 8,
            EndHour = 17,
            Daily = true,
            Priority = priority,
            RegionId = Guid.NewGuid()
        };
    }

    public static WorldEvent MakeWorldEvent(Guid worldId, Guid? regionId = null) {
        return new WorldEvent {
            WorldId = worldId,
            Description = "A test world event",
            Date = DateTime.UtcNow,
            Tags = [],
            RegionId = regionId
        };
    }
}