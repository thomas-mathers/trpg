using TRPG.Models;

namespace TRPG.Tests.Helpers;

internal static class Builders
{
    public static Person MakePerson(Guid? worldId = null, Guid? raceId = null, Guid? professionId = null, Guid? birthCityId = null) => new()
    {
        Name = "Test Person",
        RaceId = raceId ?? Guid.NewGuid(),
        BirthCityId = birthCityId ?? Guid.NewGuid(),
        BirthYear = 1000,
        ProfessionId = professionId ?? Guid.NewGuid(),
        Location = new Location
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Coordinates = new Point(0, 0)
        },
        Progression = new Progression
        {
            Level = 1,
            Experience = new Meter(0, 100)
        },
        Attributes = new Attributes
        {
            Hp = new Meter(100, 100),
            Ap = new Meter(50, 50),
            Strength = 10,
            Defense = 5,
            Dexterity = 8,
            Endurance = 7,
            Intelligence = 9
        }
    };

    public static Item MakeItem(bool stackable = false) => new()
    {
        Name = $"Item-{Guid.NewGuid():N}",
        Description = "A test item",
        Category = ItemCategory.Consumable,
        IsStackable = stackable,
        Weight = 1,
        GoldValue = 10
    };

    public static Skill MakeSkill() => new()
    {
        Name = $"Skill-{Guid.NewGuid():N}",
        Description = "A test skill",
        ApCost = 5,
        CooldownTurns = 1
    };

    public static Quest MakeQuest(Guid giverId) => new()
    {
        GiverId = giverId,
        Name = $"Quest-{Guid.NewGuid():N}",
        Description = "A test quest",
        GoldReward = 100,
        ExperienceReward = 50
    };
}
