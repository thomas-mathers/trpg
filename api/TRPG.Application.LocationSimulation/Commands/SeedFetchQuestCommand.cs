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

// Finds someone at the seeding location who wants proof that several of the monsters lurking in a
// nearby dungeon have been dealt with. Repeatable the same way as SeedClearDungeonQuestCommand and
// SeedCourierQuestCommand: the giver is only excluded while the player already has an active fetch
// quest from them, not forever — CreatureSpawner refills the dungeon over time, so there's always
// another batch of trophies to collect later. No-ops at any step where nothing eligible exists.
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
    private const int TrophyCount = 3;

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
                creatureIdsByBuildingId.GetValueOrDefault(building.Id)?.Length >= TrophyCount
            )
            .ToArray();
        if (eligibleBuildings.Length == 0)
        {
            return null;
        }

        var chosenBuilding = eligibleBuildings[Random.Shared.Next(eligibleBuildings.Length)];
        var chosenCreatureIds = creatureIdsByBuildingId[chosenBuilding.Id]
            .OrderBy(_ => Random.Shared.Next())
            .Take(TrophyCount)
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

        var trophies = creatureIds
            .Select(creatureId => new Item
            {
                WorldId = command.WorldId,
                Name = $"{building.Name} Trophy",
                Description =
                    $"Proof that one of the monsters lurking in {building.Name} has been slain.",
                Quantity = 1,
                Ownership = new ItemOwnership
                {
                    OwnerId = creatureId,
                    OwnerType = OwnerType.Creature,
                },
            })
            .ToArray();
        await addItems.Handle(new AddItemsCommand { Items = trophies }, cancellationToken);

        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = $"Trophies from {building.Name}",
            Description =
                $"{giver.Name} wants proof that {trophies.Length} of the monsters in {building.Name} have been dealt with.",
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
        var objective = new GiveItemsObjective
        {
            WorldId = command.WorldId,
            QuestId = quest.Id,
            Name = $"Collect trophies from {building.Name}",
            Description =
                $"Defeat monsters in {building.Name} and bring {trophies.Length} trophies to {giver.Name}.",
            ItemIds = trophies.Select(item => item.Id).ToList(),
            RecipientId = giver.Id,
            RequiredAmount = trophies.Length,
        };

        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = [objective] },
            cancellationToken
        );

        transaction.Complete();
    }
}
