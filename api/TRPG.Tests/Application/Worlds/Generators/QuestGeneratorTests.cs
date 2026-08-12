using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public sealed class QuestGeneratorTests
{
    [Fact]
    public void Generate_CreatesLinkedQuestsWithEveryObjectiveType_WhenStateHasRequiredEntities()
    {
        // Arrange
        var world = Builders.MakeWorld();
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: world.Id);
        var cityCenter = Builders.MakeDistrict(city.Id, worldId: world.Id);
        var building = Builders.MakeBuilding(stateId, city.Id, worldId: world.Id);
        var room = Builders.MakeRoom(building.Id, worldId: world.Id);
        var giver = Builders.MakeCreature(world.Id, stateId: stateId, name: "Giver");
        var speaker = Builders.MakeCreature(world.Id, stateId: stateId, name: "Speaker");
        var firstMonster = Builders.MakeCreature(
            world.Id,
            CreatureType.Beast,
            profession: null,
            stateId: stateId,
            name: "Wolf One"
        );
        var secondMonster = Builders.MakeCreature(
            world.Id,
            CreatureType.Beast,
            profession: null,
            stateId: stateId,
            name: "Wolf Two"
        );
        var item = new Item
        {
            WorldId = world.Id,
            Name = "Wolf Pelt",
            Description = "A pelt from a wolf.",
            Ownership = new ItemOwnership
            {
                OwnerId = firstMonster.Id,
                OwnerType = OwnerType.Creature,
            },
        };
        var generator = new QuestGenerator();

        // Act
        var result = generator.Generate(
            MakeWorld(
                world,
                city,
                cityCenter,
                building,
                room,
                [giver, speaker, firstMonster, secondMonster],
                [item]
            ),
            stateId
        );

        // Assert
        Assert.Equal(2, result.Quests.Count);
        var firstQuest = result.Quests.First(quest => quest.Name == "A Dangerous Delivery");
        var secondQuest = result.Quests.First(quest => quest.Name == "The Trail Continues");
        Assert.Contains(firstQuest.Id, secondQuest.PrerequisiteQuestIds);
        Assert.Collection(
            result.Objectives,
            objective => Assert.IsType<KillCreatureObjective>(objective),
            objective => Assert.IsType<CollectItemObjective>(objective),
            objective => Assert.IsType<ExploreBuildingObjective>(objective),
            objective => Assert.IsType<KillCreatureTypeObjective>(objective),
            objective => Assert.IsType<SpeakToCreatureObjective>(objective),
            objective => Assert.IsType<ExploreCityObjective>(objective)
        );
    }

    [Fact]
    public void Generate_SkipsUnavailableObjectives_WhenStateHasOnlyOneMonster()
    {
        // Arrange
        var world = Builders.MakeWorld();
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: world.Id);
        var cityCenter = Builders.MakeDistrict(city.Id, worldId: world.Id);
        var building = Builders.MakeBuilding(stateId, city.Id, worldId: world.Id);
        var room = Builders.MakeRoom(building.Id, worldId: world.Id);
        var giver = Builders.MakeCreature(world.Id, stateId: stateId);
        var speaker = Builders.MakeCreature(world.Id, stateId: stateId);
        var monster = Builders.MakeCreature(
            world.Id,
            CreatureType.Beast,
            profession: null,
            stateId: stateId
        );
        var generator = new QuestGenerator();

        // Act
        var result = generator.Generate(
            MakeWorld(world, city, cityCenter, building, room, [giver, speaker, monster], []),
            stateId
        );

        // Assert
        Assert.Equal(2, result.Quests.Count);
        Assert.Contains(result.Objectives, objective => objective is KillCreatureObjective);
        Assert.Contains(result.Objectives, objective => objective is KillCreatureTypeObjective);
        Assert.DoesNotContain(result.Objectives, objective => objective is CollectItemObjective);
    }

    private static WorldGeneratorResult MakeWorld(
        World world,
        City city,
        District cityCenter,
        Building building,
        Room room,
        IReadOnlyList<Creature> creatures,
        IReadOnlyList<Item> items
    ) =>
        new()
        {
            BuildingOwners = [],
            Buildings = [building],
            Cities = [city],
            Countries = [],
            Creatures = creatures,
            Districts = [cityCenter],
            FactionMembers = [],
            Factions = [],
            Items = items,
            Jobs = [],
            Knowledge = [],
            Locations = [],
            Props = [],
            Relationships = [],
            Roads = [],
            LocationConnectorKeys = [],
            Rooms = [room],
            Skills = [],
            States = [],
            World = world,
        };
}
