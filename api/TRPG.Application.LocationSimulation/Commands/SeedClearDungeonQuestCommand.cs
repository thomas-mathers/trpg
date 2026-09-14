using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedClearDungeonQuestCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
}

// Finds someone at the seeding location who wants a nearby dungeon cleared of its monsters. Unlike
// the captive-rescue quest, the same dungeon can be offered again once its current clear-quest
// (if any) is no longer active — CreatureSpawner refills a dungeon's population over time, so
// clearing it is worth doing again later. No-ops at any step where nothing eligible exists.
internal class SeedClearDungeonQuestCommandHandler(
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
        IReadOnlyList<Guid>
    > getGiverCandidateIds,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetActiveClearLocationObjectiveBuildingIdsQuery,
        IReadOnlySet<Guid>
    > getActiveClearLocationObjectiveBuildingIds,
    IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>> getRoomsByBuildingIds,
    IQueryHandler<
        GetLivingHostileCreatureCountsByLocationQuery,
        IReadOnlyDictionary<Guid, int>
    > getLivingHostileCreatureCountsByLocation,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedClearDungeonQuestCommand, bool>
{
    private const int GoldReward = 60;
    private const int GiverReputationReward = 15;

    public async Task<bool> Handle(
        SeedClearDungeonQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var giver = await FindGiver(command, cancellationToken);
        if (giver == null)
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

        var candidate = await FindEligibleDungeon(
            command,
            giverLocation.StateId,
            cancellationToken
        );
        if (candidate == null)
        {
            return false;
        }

        var (building, livingCount) = candidate.Value;

        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = $"Clear Out {building.Name}",
            Description = $"{building.Name} has drawn monsters again. Clear them out for a reward.",
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
        var objective = new ClearLocationObjective
        {
            WorldId = command.WorldId,
            QuestId = quest.Id,
            Name = $"Clear {building.Name}",
            Description = $"Defeat the monsters lurking in {building.Name}.",
            BuildingId = building.Id,
            LocationId = building.ExteriorLocationId,
            RequiredAmount = livingCount,
        };

        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = [objective] },
            cancellationToken
        );

        return true;
    }

    private async Task<Creature?> FindGiver(
        SeedClearDungeonQuestCommand command,
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

    private async Task<(Building Building, int LivingCount)?> FindEligibleDungeon(
        SeedClearDungeonQuestCommand command,
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
        var activeBuildingIds = await getActiveClearLocationObjectiveBuildingIds.Handle(
            new GetActiveClearLocationObjectiveBuildingIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        var candidates = dungeonBuildings
            .Where(building =>
                exteriorLocationsById.TryGetValue(building.ExteriorLocationId, out var location)
                && location.StateId == stateId
            )
            .Where(building => !activeBuildingIds.Contains(building.Id))
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

        var livingCountsByLocationId = await getLivingHostileCreatureCountsByLocation.Handle(
            new GetLivingHostileCreatureCountsByLocationQuery
            {
                WorldId = command.WorldId,
                LocationIds = buildingIdByLocationId.Keys.ToArray(),
            },
            cancellationToken
        );
        var livingCountByBuildingId = livingCountsByLocationId
            .GroupBy(countByLocationId => buildingIdByLocationId[countByLocationId.Key])
            .ToDictionary(
                group => group.Key,
                group => group.Sum(countByLocationId => countByLocationId.Value)
            );

        var eligibleBuildings = candidates
            .Where(building => livingCountByBuildingId.GetValueOrDefault(building.Id) > 0)
            .ToArray();
        if (eligibleBuildings.Length == 0)
        {
            return null;
        }

        var chosen = eligibleBuildings[Random.Shared.Next(eligibleBuildings.Length)];
        return (chosen, livingCountByBuildingId[chosen.Id]);
    }
}
