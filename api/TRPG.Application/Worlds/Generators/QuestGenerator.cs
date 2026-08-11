using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public record QuestGeneratorResult(
    IReadOnlyCollection<Quest> Quests,
    IReadOnlyCollection<QuestObjective> Objectives
);

public class QuestGenerator
{
    public QuestGeneratorResult Generate(WorldGeneratorResult world, Guid stateId)
    {
        var residents = world
            .Creatures.Where(creature =>
                creature.StateId == stateId
                && CreatureTypes.Humanoid.Contains(creature.CreatureType)
            )
            .Take(2)
            .ToArray();
        var city = world.Cities.FirstOrDefault(city => city.StateId == stateId);
        var cityCenter = city is null
            ? null
            : world.Districts.FirstOrDefault(district =>
                district.CityId == city.Id && district.DistrictType == DistrictType.CityCenter
            );
        var building = world.Buildings.FirstOrDefault(building =>
            building.StateId == stateId && building.CityId is not null
        );
        var buildingLocation = building is null
            ? null
            : world.Rooms.FirstOrDefault(room => room.BuildingId == building.Id)?.LocationId;
        var monstersByType = world
            .Creatures.Where(creature =>
                creature.StateId == stateId
                && !CreatureTypes.Humanoid.Contains(creature.CreatureType)
            )
            .GroupBy(creature => creature.CreatureType)
            .FirstOrDefault(group => group.Count() >= 2)
            ?.ToArray();

        if (
            residents.Length < 2
            || city is null
            || cityCenter is null
            || building is null
            || buildingLocation is null
            || monstersByType is null
        )
        {
            return new QuestGeneratorResult([], []);
        }

        var assassinationTarget = monstersByType[0];
        var creatureTypeTarget = monstersByType[1];
        var item = world.Items.FirstOrDefault(item =>
            item.Ownership.OwnerId == assassinationTarget.Id
        );
        if (item is null)
        {
            return new QuestGeneratorResult([], []);
        }

        var giver = residents[0];
        var speaker = residents[1];
        var firstQuest = new Quest
        {
            WorldId = world.World.Id,
            GiverId = giver.Id,
            Name = "A Dangerous Delivery",
            Description = $"Recover {item.Name} from {assassinationTarget.Name}.",
            GoldReward = 100,
        };
        var secondQuest = new Quest
        {
            WorldId = world.World.Id,
            GiverId = giver.Id,
            Name = "The Trail Continues",
            Description = $"Follow the remaining leads for {giver.Name}.",
            GoldReward = 150,
            PrerequisiteQuestIds = [firstQuest.Id],
        };

        return new QuestGeneratorResult(
            Quests: [firstQuest, secondQuest],
            Objectives:
            [
                new KillCreatureObjective
                {
                    WorldId = world.World.Id,
                    QuestId = firstQuest.Id,
                    Name = $"Defeat {assassinationTarget.Name}",
                    Description = $"Defeat {assassinationTarget.Name}.",
                    CreatureId = assassinationTarget.Id,
                    LocationId = assassinationTarget.LocationId,
                },
                new CollectItemObjective
                {
                    WorldId = world.World.Id,
                    QuestId = firstQuest.Id,
                    Name = $"Recover {item.Name}",
                    Description = $"Recover {item.Name}.",
                    ItemId = item.Id,
                    LocationId = assassinationTarget.LocationId,
                    RequiredAmount = 1,
                },
                new ExploreBuildingObjective
                {
                    WorldId = world.World.Id,
                    QuestId = firstQuest.Id,
                    Name = $"Visit {building.Name}",
                    Description = $"Visit {building.Name}.",
                    BuildingId = building.Id,
                    LocationId = buildingLocation,
                },
                new KillCreatureTypeObjective
                {
                    WorldId = world.World.Id,
                    QuestId = secondQuest.Id,
                    Name = $"Defeat a {creatureTypeTarget.CreatureType}",
                    Description = $"Defeat a {creatureTypeTarget.CreatureType}.",
                    CreatureType = creatureTypeTarget.CreatureType,
                    LocationId = creatureTypeTarget.LocationId,
                    RequiredAmount = 1,
                },
                new SpeakToCreatureObjective
                {
                    WorldId = world.World.Id,
                    QuestId = secondQuest.Id,
                    Name = $"Speak to {speaker.Name}",
                    Description = $"Speak to {speaker.Name}.",
                    CreatureId = speaker.Id,
                    LocationId = speaker.LocationId,
                },
                new ExploreCityObjective
                {
                    WorldId = world.World.Id,
                    QuestId = secondQuest.Id,
                    Name = $"Visit {city.Name}",
                    Description = $"Visit {city.Name}.",
                    CityId = city.Id,
                    LocationId = cityCenter.LocationId,
                },
            ]
        );
    }
}
