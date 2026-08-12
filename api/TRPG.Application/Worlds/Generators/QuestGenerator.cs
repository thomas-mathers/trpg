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
            .ToArray();
        var giver = residents.FirstOrDefault();

        if (giver is null)
        {
            return new QuestGeneratorResult([], []);
        }

        var firstQuest = CreateFirstQuest(world.World.Id, giver.Id);
        var firstObjectives = CreateFirstObjectives(world, stateId, firstQuest.Id);
        var secondQuest = CreateSecondQuest(
            world.World.Id,
            giver.Id,
            firstObjectives.Count > 0 ? [firstQuest.Id] : []
        );
        var secondObjectives = CreateSecondObjectives(world, stateId, residents, secondQuest.Id);
        var quests = new List<Quest>();
        var objectives = new List<QuestObjective>();

        if (firstObjectives.Count > 0)
        {
            quests.Add(firstQuest);
            objectives.AddRange(firstObjectives);
        }

        if (secondObjectives.Count > 0)
        {
            quests.Add(secondQuest);
            objectives.AddRange(secondObjectives);
        }

        return new QuestGeneratorResult(quests, objectives);
    }

    private static Quest CreateFirstQuest(Guid worldId, Guid giverId) =>
        new()
        {
            WorldId = worldId,
            GiverId = giverId,
            Name = "A Dangerous Delivery",
            Description = "Help with a dangerous errand.",
            GoldReward = 100,
        };

    private static Quest CreateSecondQuest(
        Guid worldId,
        Guid giverId,
        IReadOnlyCollection<Guid> prerequisiteQuestIds
    ) =>
        new()
        {
            WorldId = worldId,
            GiverId = giverId,
            Name = "The Trail Continues",
            Description = "Follow the remaining leads.",
            GoldReward = 150,
            PrerequisiteQuestIds = prerequisiteQuestIds.ToList(),
        };

    private static List<QuestObjective> CreateFirstObjectives(
        WorldGeneratorResult world,
        Guid stateId,
        Guid questId
    )
    {
        var objectives = new List<QuestObjective>();
        var target = world.Creatures.FirstOrDefault(creature =>
            creature.StateId == stateId && !CreatureTypes.Humanoid.Contains(creature.CreatureType)
        );

        if (target is not null)
        {
            objectives.Add(
                new KillCreatureObjective
                {
                    WorldId = world.World.Id,
                    QuestId = questId,
                    Name = $"Defeat {target.Name}",
                    Description = $"Defeat {EntityReference(target.Name, "Creature", target.Id)}.",
                    CreatureId = target.Id,
                    LocationId = target.LocationId,
                }
            );

            var item = world.Items.FirstOrDefault(item => item.Ownership.OwnerId == target.Id);
            if (item is not null)
            {
                objectives.Add(
                    new CollectItemObjective
                    {
                        WorldId = world.World.Id,
                        QuestId = questId,
                        Name = $"Recover {item.Name}",
                        Description = $"Recover {EntityReference(item.Name, "Item", item.Id)}.",
                        ItemId = item.Id,
                        LocationId = target.LocationId,
                    }
                );
            }
        }

        var building = world.Buildings.FirstOrDefault(building =>
            building.StateId == stateId && building.CityId is not null
        );
        var locationId = building is null
            ? null
            : world.Rooms.FirstOrDefault(room => room.BuildingId == building.Id)?.LocationId;

        if (building is not null && locationId is not null)
        {
            objectives.Add(
                new ExploreBuildingObjective
                {
                    WorldId = world.World.Id,
                    QuestId = questId,
                    Name = $"Visit {building.Name}",
                    Description =
                        $"Visit {EntityReference(building.Name, "Building", building.Id)}.",
                    BuildingId = building.Id,
                    LocationId = locationId,
                }
            );
        }

        return objectives;
    }

    private static List<QuestObjective> CreateSecondObjectives(
        WorldGeneratorResult world,
        Guid stateId,
        IReadOnlyList<Creature> residents,
        Guid questId
    )
    {
        var objectives = new List<QuestObjective>();
        var monsterGroup = world
            .Creatures.Where(creature =>
                creature.StateId == stateId
                && !CreatureTypes.Humanoid.Contains(creature.CreatureType)
            )
            .GroupBy(creature => creature.CreatureType)
            .FirstOrDefault()
            ?.ToArray();

        if (monsterGroup is not null)
        {
            var target = monsterGroup[0];
            objectives.Add(
                new KillCreatureTypeObjective
                {
                    WorldId = world.World.Id,
                    QuestId = questId,
                    Name = $"Defeat a {target.CreatureType}",
                    Description = $"Defeat a {target.CreatureType}.",
                    CreatureType = target.CreatureType,
                    LocationId = target.LocationId,
                }
            );
        }

        var speaker = residents.Skip(1).FirstOrDefault();
        if (speaker is not null)
        {
            objectives.Add(
                new SpeakToCreatureObjective
                {
                    WorldId = world.World.Id,
                    QuestId = questId,
                    Name = $"Speak to {speaker.Name}",
                    Description =
                        $"Speak to {EntityReference(speaker.Name, "Creature", speaker.Id)}.",
                    CreatureId = speaker.Id,
                    LocationId = speaker.LocationId,
                }
            );
        }

        var city = world.Cities.FirstOrDefault(city => city.StateId == stateId);
        var cityCenter = city is null
            ? null
            : world.Districts.FirstOrDefault(district =>
                district.CityId == city.Id && district.DistrictType == DistrictType.CityCenter
            );

        if (city is not null && cityCenter is not null)
        {
            objectives.Add(
                new ExploreCityObjective
                {
                    WorldId = world.World.Id,
                    QuestId = questId,
                    Name = $"Visit {city.Name}",
                    Description = $"Visit {EntityReference(city.Name, "City", city.Id)}.",
                    CityId = city.Id,
                    LocationId = cityCenter.LocationId,
                }
            );
        }

        return objectives;
    }

    private static string EntityReference(string name, string entityType, Guid id) =>
        $"[{name}](entity://{entityType}/{id})";
}
