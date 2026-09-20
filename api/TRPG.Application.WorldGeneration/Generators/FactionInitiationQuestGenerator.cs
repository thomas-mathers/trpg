using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal class FactionInitiationQuestGeneratorInput
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Faction> Factions { get; init; }
    public required IReadOnlyCollection<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyCollection<Building> Buildings { get; init; }
    public required IReadOnlyCollection<Room> Rooms { get; init; }
    public required IReadOnlyCollection<Location> Locations { get; init; }
    public required IReadOnlyCollection<Creature> Creatures { get; init; }
    public required IReadOnlyCollection<Prop> Props { get; init; }
}

internal record FactionInitiationQuestGeneratorResult(
    IReadOnlyCollection<Quest> Quests,
    IReadOnlyCollection<QuestObjective> Objectives,
    IReadOnlyCollection<Item> Items
);

internal static class FactionInitiationQuestGenerator
{
    private const int GoldReward = 50;

    public static FactionInitiationQuestGeneratorResult Generate(
        FactionInitiationQuestGeneratorInput input
    )
    {
        var quests = new List<Quest>();
        var objectives = new List<QuestObjective>();
        var items = new List<Item>();
        var locationsById = input.Locations.ToDictionary(location => location.Id);
        var memberIdsByFactionId = input
            .FactionMembers.GroupBy(member => member.FactionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(member => member.CreatureId).ToHashSet()
            );

        foreach (
            var faction in input.Factions.Where(faction => faction.Kind == FactionKind.Joinable)
        )
        {
            var hall = input.Buildings.SingleOrDefault(building =>
                building.BuildingType == BuildingType.GuildHall && building.FactionId == faction.Id
            );
            if (hall == null || !memberIdsByFactionId.TryGetValue(faction.Id, out var memberIds))
            {
                continue;
            }

            var leaderId = input
                .FactionMembers.Where(member =>
                    member.FactionId == faction.Id && member.Role == FactionRole.Leader
                )
                .Select(member => member.CreatureId)
                .Single();
            var quest = MakeQuest(input.WorldId, faction, leaderId);
            quests.Add(quest);

            if (faction.Name == FactionNames.HollowCoin)
            {
                var target = FindNearbyTarget(input, hall, memberIds, locationsById);
                var item = MakeItem(
                    input.WorldId,
                    "Stamped Silver Ledger",
                    target.Id,
                    OwnerType.Creature
                );
                items.Add(item);
                objectives.Add(
                    new GiveItemsObjective
                    {
                        WorldId = input.WorldId,
                        QuestId = quest.Id,
                        Name = "Steal the planted ledger",
                        Description =
                            $"Take the marked ledger from {target.Name} and return it unseen.",
                        ItemIds = [item.Id],
                        RecipientId = leaderId,
                    }
                );
            }
            else if (faction.Name == FactionNames.ArcaneConclave)
            {
                var hallLocationIds = input
                    .Rooms.Where(room => room.BuildingId == hall.Id)
                    .Select(room => room.LocationId)
                    .ToHashSet();
                var container = input
                    .Props.OfType<Container>()
                    .FirstOrDefault(prop => hallLocationIds.Contains(prop.LocationId));
                var ownerId = container?.Id ?? leaderId;
                var ownerType = container == null ? OwnerType.Creature : OwnerType.Container;
                var item = MakeItem(input.WorldId, "Sealed Conclave Focus", ownerId, ownerType);
                items.Add(item);
                objectives.Add(
                    new CollectItemObjective
                    {
                        WorldId = input.WorldId,
                        QuestId = quest.Id,
                        Name = "Retrieve the sealed focus",
                        Description =
                            "Retrieve the regulated focus from the Conclave archive under supervision.",
                        ItemId = item.Id,
                    }
                );
            }
            else
            {
                var target = FindNearbyThreat(input, hall, memberIds, locationsById);
                objectives.Add(
                    new KillCreatureObjective
                    {
                        WorldId = input.WorldId,
                        QuestId = quest.Id,
                        Name = "Eliminate the chosen threat",
                        Description =
                            $"Prove yourself by eliminating {target.Name} near the guild's territory.",
                        CreatureId = target.Id,
                    }
                );
            }
        }

        return new FactionInitiationQuestGeneratorResult(quests, objectives, items);
    }

    private static Quest MakeQuest(Guid worldId, Faction faction, Guid giverId) =>
        new()
        {
            WorldId = worldId,
            GiverId = giverId,
            Name = $"Initiation: {faction.Name}",
            Description = $"Complete {faction.Name}'s initiation and earn membership.",
            GoldReward = GoldReward,
            MembershipRewardFactionId = faction.Id,
        };

    private static Creature FindNearbyTarget(
        FactionInitiationQuestGeneratorInput input,
        Building hall,
        IReadOnlySet<Guid> excludedIds,
        IReadOnlyDictionary<Guid, Location> locationsById
    )
    {
        var hallLocation = locationsById[hall.ExteriorLocationId];
        return input.Creatures.First(creature =>
            !excludedIds.Contains(creature.Id)
            && locationsById.TryGetValue(creature.LocationId, out var location)
            && location.CityId == hallLocation.CityId
        );
    }

    private static Creature FindNearbyThreat(
        FactionInitiationQuestGeneratorInput input,
        Building hall,
        IReadOnlySet<Guid> excludedIds,
        IReadOnlyDictionary<Guid, Location> locationsById
    )
    {
        var hallLocation = locationsById[hall.ExteriorLocationId];
        var factionsById = input.Factions.ToDictionary(faction => faction.Id);
        var memberIds = input
            .FactionMembers.Where(member =>
                factionsById[member.FactionId].Kind == FactionKind.Wilderness
            )
            .Select(member => member.CreatureId)
            .ToHashSet();
        var creaturesById = input.Creatures.ToDictionary(creature => creature.Id);
        return memberIds
            .Select(id => creaturesById[id])
            .First(creature =>
                !excludedIds.Contains(creature.Id)
                && locationsById.TryGetValue(creature.LocationId, out var location)
                && location.StateId == hallLocation.StateId
            );
    }

    private static Item MakeItem(Guid worldId, string name, Guid ownerId, OwnerType ownerType) =>
        new()
        {
            WorldId = worldId,
            Name = name,
            Description = "A valuable planted specifically for a faction initiation trial.",
            GoldValue = 100,
            Quantity = 1,
            Ownership = new ItemOwnership { OwnerId = ownerId, OwnerType = ownerType },
        };
}
