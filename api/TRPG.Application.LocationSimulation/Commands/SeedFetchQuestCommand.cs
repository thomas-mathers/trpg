using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedFetchQuestCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

// Finds someone at the seeding location who wants a few gatherable materials brought back from a
// nearby dungeon. Repeatable the same way as SeedClearDungeonQuestCommand and SeedCourierQuestCommand:
// the giver is only excluded while the player already has an active fetch quest from them, not
// forever — CreatureSpawner refills the dungeon over time, so there's always more to gather later.
// No-ops at any step where nothing eligible exists.
internal class SeedFetchQuestCommandHandler(
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
        IReadOnlyList<Guid>
    > getGiverCandidateIds,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetActiveGiveItemsObjectiveRecipientIdsQuery,
        IReadOnlySet<Guid>
    > getActiveRecipientIds,
    IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>> getRoomsByBuildingIds,
    IQueryHandler<
        GetLivingHostileCreatureIdsByLocationQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getLivingHostileCreatureIdsByLocation,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedFetchQuestCommand, bool>
{
    private const int GoldReward = 45;
    private const int GiverReputationReward = 12;
    private const int ItemCount = 3;

    // Classic gather-quest materials by creature type — what a player would recognize as loot
    // worth turning in, without the quest text needing to say anything got killed for it.
    private static readonly Dictionary<CreatureType, string> MaterialByCreatureType = new()
    {
        [CreatureType.Beast] = "Pelt",
        [CreatureType.Orc] = "Tusk",
        [CreatureType.Goblin] = "Ear",
        [CreatureType.Undead] = "Bone",
        [CreatureType.Demon] = "Horn",
        [CreatureType.Construct] = "Gear",
        [CreatureType.Elemental] = "Core",
        [CreatureType.Wraith] = "Essence",
        [CreatureType.Giant] = "Tooth",
        [CreatureType.Dragon] = "Scale",
    };
    private const string DefaultMaterial = "Remnant";

    public async Task<bool> Handle(
        SeedFetchQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var giver = await FindGiver(command, cancellationToken);
        if (giver == null)
        {
            return false;
        }

        var activeRecipientIds = await getActiveRecipientIds.Handle(
            new GetActiveGiveItemsObjectiveRecipientIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );
        if (activeRecipientIds.Contains(giver.Id))
        {
            return false;
        }

        var giverLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (giverLocation == null)
        {
            return false;
        }

        var quarry = await FindEligibleQuarry(command, giverLocation.StateId, cancellationToken);
        if (quarry == null)
        {
            return false;
        }

        var (building, creatureIds) = quarry.Value;

        await CreateFetchQuest(command, giver, building, creatureIds, cancellationToken);

        return true;
    }

    private async Task<Creature?> FindGiver(
        SeedFetchQuestCommand command,
        CancellationToken cancellationToken
    )
    {
        var candidateGiverIds = await getGiverCandidateIds.Handle(
            new GetCreatureIdsWithCreatureJobInLocationQuery { LocationId = command.LocationId },
            cancellationToken
        );
        var candidateGivers = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = candidateGiverIds },
            cancellationToken
        );

        return candidateGivers
            .Values.Where(creature => CreatureTypes.Humanoid.Contains(creature.CreatureType))
            .FirstOrDefault();
    }

    private async Task<(Building Building, IReadOnlyList<Guid> CreatureIds)?> FindEligibleQuarry(
        SeedFetchQuestCommand command,
        Guid stateId,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var dungeonBuildings = buildings
            .Where(building => DungeonPopulator.SupportsDungeonType(building.BuildingType))
            .ToArray();
        if (dungeonBuildings.Length == 0)
        {
            return null;
        }

        var exteriorLocationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = dungeonBuildings.Select(building => building.ExteriorLocationId).ToArray(),
            },
            cancellationToken
        );

        var candidates = dungeonBuildings
            .Where(building =>
                exteriorLocationsById.TryGetValue(building.ExteriorLocationId, out var location)
                && location.StateId == stateId
            )
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var candidateBuildingIds = candidates.Select(building => building.Id).ToArray();
        var rooms = await getRoomsByBuildingIds.Handle(
            new GetRoomsByBuildingIdsQuery { BuildingIds = candidateBuildingIds },
            cancellationToken
        );
        var buildingIdByLocationId = rooms.ToDictionary(
            room => room.LocationId,
            room => room.BuildingId
        );

        var creatureIdsByLocationId = await getLivingHostileCreatureIdsByLocation.Handle(
            new GetLivingHostileCreatureIdsByLocationQuery
            {
                WorldId = command.WorldId,
                LocationIds = buildingIdByLocationId.Keys.ToArray(),
            },
            cancellationToken
        );
        var creatureIdsByBuildingId = creatureIdsByLocationId
            .GroupBy(creatureIdsAtLocation => buildingIdByLocationId[creatureIdsAtLocation.Key])
            .ToDictionary(
                group => group.Key,
                group =>
                    group.SelectMany(creatureIdsAtLocation => creatureIdsAtLocation.Value).ToArray()
            );

        var eligibleBuildings = candidates
            .Where(building =>
                creatureIdsByBuildingId.GetValueOrDefault(building.Id)?.Length >= ItemCount
            )
            .ToArray();
        if (eligibleBuildings.Length == 0)
        {
            return null;
        }

        var chosenBuilding = eligibleBuildings[Random.Shared.Next(eligibleBuildings.Length)];
        var chosenCreatureIds = creatureIdsByBuildingId[chosenBuilding.Id]
            .OrderBy(_ => Random.Shared.Next())
            .Take(ItemCount)
            .ToArray();

        return (chosenBuilding, chosenCreatureIds);
    }

    private async Task CreateFetchQuest(
        SeedFetchQuestCommand command,
        Creature giver,
        Building building,
        IReadOnlyList<Guid> creatureIds,
        CancellationToken cancellationToken
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        var materials = creatureIds
            .Select(creatureId =>
                MaterialByCreatureType.GetValueOrDefault(
                    creaturesById[creatureId].CreatureType,
                    DefaultMaterial
                )
            )
            .ToArray();
        var drops = creatureIds
            .Zip(
                materials,
                (creatureId, material) =>
                    new Item
                    {
                        WorldId = command.WorldId,
                        Name = $"{creaturesById[creatureId].CreatureType} {material}",
                        Description =
                            $"A {material.ToLowerInvariant()} recovered from a creature in {building.Name}.",
                        Quantity = 1,
                        Ownership = new ItemOwnership
                        {
                            OwnerId = creatureId,
                            OwnerType = OwnerType.Creature,
                        },
                    }
            )
            .ToArray();
        await addItems.Handle(new AddItemsCommand { Items = drops }, cancellationToken);

        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = $"Gathering in {building.Name}",
            Description = $"{giver.Name} could use a few things gathered from {building.Name}.",
            GoldReward = GoldReward,
        };
        quest.ReputationRewards.Add(
            new QuestReputationReward
            {
                WorldId = command.WorldId,
                QuestId = quest.Id,
                TargetId = giver.Id,
                TargetType = ReputationTargetType.Creature,
                Score = GiverReputationReward,
            }
        );
        var objectives = drops
            .GroupBy(drop => drop.Name)
            .Select(group => new GiveItemKindObjective
            {
                WorldId = command.WorldId,
                QuestId = quest.Id,
                Name =
                    group.Count() > 1
                        ? $"Recover {group.Count()}x {group.Key}"
                        : $"Recover a {group.Key}",
                Description =
                    group.Count() > 1
                        ? $"Search {building.Name} for {group.Count()} {group.Key} drops and bring them to {giver.Name}."
                        : $"Search {building.Name} for a {group.Key} and bring it to {giver.Name}.",
                ItemName = group.Key,
                RecipientId = giver.Id,
                RequiredAmount = group.Count(),
            })
            .ToArray();

        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = objectives },
            cancellationToken
        );

        transaction.Complete();
    }
}
