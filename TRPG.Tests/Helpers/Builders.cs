using TRPG.Models;

namespace TRPG.Tests.Helpers;

internal static class Builders {
    public static Person MakePerson(Guid? worldId = null, Guid? raceId = null, Guid? professionId = null,
        Guid? birthCityId = null) {
        return new Person {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = "Test Person",
            RaceId = raceId ?? Guid.NewGuid(),
            BirthCityId = birthCityId ?? Guid.NewGuid(),
            BirthYear = 1000,
            ProfessionId = professionId ?? Guid.NewGuid(),
            Location = new Location { Coordinates = new Point(0, 0) },
            Progression = new Progression {
                Level = 1,
                Experience = new Meter(0, 100)
            },
            Attributes = new Attributes {
                Hp = new Meter(100, 100),
                Ap = new Meter(50, 50),
                Strength = 10,
                Defense = 5,
                Dexterity = 8,
                Endurance = 7,
                Intelligence = 9
            }
        };
    }

    public static Item MakeItem(Guid? worldId = null, bool stackable = false) {
        return new Item {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test item",
            Category = ItemCategory.Consumable,
            IsStackable = stackable,
            Weight = 1,
            GoldValue = 10
        };
    }

    public static Attack MakeSkill(Guid? worldId = null) {
        return new Attack {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Skill-{Guid.NewGuid():N}",
            Description = "A test skill",
            Cost = 5,
            Cooldown = 1,
            TargetType = TargetType.Single,
            DamageType = DamageType.Physical,
            DamageAmountType = AmountType.Flat,
            DamageAmount = 10
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

    public static Profession MakeProfession(Guid? worldId = null) {
        return new Profession {
            WorldId = worldId ?? Guid.NewGuid(), Name = $"Profession-{Guid.NewGuid():N}",
            Description = "A test profession"
        };
    }

    public static Faction MakeFaction(Guid? worldId = null) {
        return new Faction {
            WorldId = worldId ?? Guid.NewGuid(), Name = $"Faction-{Guid.NewGuid():N}", Description = "A test faction"
        };
    }

    public static World MakeWorld() {
        return new World { Name = $"World-{Guid.NewGuid():N}", Description = "A test world", Boundary = new Rectangle(0, 0, 10000, 10000) };
    }

    public static Country MakeCountry(Guid worldId) {
        return new Country {
            WorldId = worldId,
            Name = $"Country-{Guid.NewGuid():N}",
            Description = "A test country",
            Boundary = new Rectangle(0, 0, 3000, 3000)
        };
    }

    public static City MakeCity(Guid countryId) {
        return new City {
            CountryId = countryId,
            Name = $"City-{Guid.NewGuid():N}",
            Description = "A test city",
            Width = 100,
            Height = 100,
            Boundary = new Rectangle(0, 0, 100, 100)
        };
    }

    public static Building MakeBuilding(Guid cityId) {
        return new Building {
            CityId = cityId,
            Name = $"Building-{Guid.NewGuid():N}",
            Description = "A test building",
            Boundary = new Rectangle(0, 0, 10, 10)
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
            Location = new Location { Coordinates = new Point(0, 0) }
        };
    }

    public static WorldEvent MakeWorldEvent(Guid worldId, Point? at = null) {
        return new WorldEvent {
            WorldId = worldId,
            Description = "A test world event",
            Date = DateTime.UtcNow,
            Tags = [],
            Region = new Circle { Center = new Location { Coordinates = at ?? new Point(0, 0) }, Radius = 100 }
        };
    }
}