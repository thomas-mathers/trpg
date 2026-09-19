using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedLlmQuestChainCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
}

// Finds someone at the seeding location willing to point the player toward a bigger story, then
// gathers a bounded pool of real entities (giver candidates, nearby dungeon buildings and their
// hostiles, and notable non-dungeon city buildings) and enqueues the slow LLM-authored chain
// generation as a background job — never calls the LLM inline, since a single generation call has
// been measured at 50-70 seconds. No-ops at any step where nothing eligible exists, same contract
// as the other Seed*QuestCommand types.
internal class SeedLlmQuestChainCommandHandler(
    ILocationSimulationDbContext context,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationsQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithCreatureJobInLocations,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>> getRoomsByBuildingIds,
    IQueryHandler<
        GetLivingHostileCreatureIdsByLocationQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getLivingHostileCreatureIdsByLocation,
    IQuestChainGenerationScheduler scheduler
) : ICommandHandler<SeedLlmQuestChainCommand, bool>
{
    private const int ChainLength = 32;
    private const int MaximumGiverCandidates = 6;
    private const int MaximumDungeonBuildings = 2;
    private const int MaximumHostilesPerDungeon = 4;
    private const int MaximumCityBuildings = 3;
    private const int MinimumEntityPoolSize = 4;

    public async Task<bool> Handle(
        SeedLlmQuestChainCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var hasPendingRequest = await context.QuestChainGenerationRequests.AnyAsync(
            request =>
                request.WorldId == command.WorldId
                && request.PlayerId == command.PlayerId
                && (
                    request.Status == QuestChainGenerationStatus.Pending
                    || request.Status == QuestChainGenerationStatus.InProgress
                ),
            cancellationToken
        );
        if (hasPendingRequest)
        {
            return false;
        }

        var entranceLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (entranceLocation?.CityId == null)
        {
            return false;
        }

        var entities = new List<QuestChainCandidateEntity>();
        entities.AddRange(
            await GatherGiverCandidates(entranceLocation.CityId.Value, cancellationToken)
        );
        entities.AddRange(
            await GatherDungeonEntities(
                command.WorldId,
                entranceLocation.StateId,
                cancellationToken
            )
        );
        entities.AddRange(
            await GatherCityBuildings(
                command.WorldId,
                entranceLocation.CityId.Value,
                cancellationToken
            )
        );

        if (entities.Count < MinimumEntityPoolSize)
        {
            return false;
        }

        var request = new QuestChainGenerationRequest
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
        };
        context.QuestChainGenerationRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        await scheduler.ScheduleAsync(
            new GenerateQuestChainCommand
            {
                RequestId = request.Id,
                ChainPremise =
                    "A new multi-step story is unfolding, drawing on the people and dangers in and around this city.",
                ChainLength = ChainLength,
                AvailableEntities = entities,
            },
            cancellationToken
        );

        return true;
    }

    private async Task<IReadOnlyList<QuestChainCandidateEntity>> GatherGiverCandidates(
        Guid cityId,
        CancellationToken cancellationToken
    )
    {
        var cityLocationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = cityId },
            cancellationToken
        );
        var candidateIds = await getCreatureIdsWithCreatureJobInLocations.Handle(
            new GetCreatureIdsWithCreatureJobInLocationsQuery { LocationIds = cityLocationIds },
            cancellationToken
        );
        var candidates = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = candidateIds },
            cancellationToken
        );

        return candidates
            .Values.Where(creature => CreatureTypes.Humanoid.Contains(creature.CreatureType))
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaximumGiverCandidates)
            .Select(creature => new QuestChainCandidateEntity(
                creature.Id,
                creature.Name,
                QuestChainEntityTypes.Creature,
                DescribeCreature(creature)
            ))
            .ToArray();
    }

    private async Task<IReadOnlyList<QuestChainCandidateEntity>> GatherDungeonEntities(
        Guid worldId,
        Guid stateId,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var dungeonBuildings = buildings
            .Where(building => DungeonPopulator.SupportsDungeonType(building.BuildingType))
            .ToArray();
        if (dungeonBuildings.Length == 0)
        {
            return [];
        }

        var exteriorLocationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = dungeonBuildings.Select(building => building.ExteriorLocationId).ToArray(),
            },
            cancellationToken
        );
        var candidateBuildings = dungeonBuildings
            .Where(building =>
                exteriorLocationsById.TryGetValue(building.ExteriorLocationId, out var location)
                && location.StateId == stateId
            )
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaximumDungeonBuildings)
            .ToArray();
        if (candidateBuildings.Length == 0)
        {
            return [];
        }

        var candidateBuildingIds = candidateBuildings.Select(building => building.Id).ToArray();
        var rooms = await getRoomsByBuildingIds.Handle(
            new GetRoomsByBuildingIdsQuery { BuildingIds = candidateBuildingIds },
            cancellationToken
        );
        var roomLocationIds = rooms.Select(room => room.LocationId).ToArray();

        var hostilesByLocation = await getLivingHostileCreatureIdsByLocation.Handle(
            new GetLivingHostileCreatureIdsByLocationQuery
            {
                WorldId = worldId,
                LocationIds = roomLocationIds,
            },
            cancellationToken
        );
        var hostileCreatureIds = hostilesByLocation
            .Values.SelectMany(ids => ids)
            .Distinct()
            .Take(MaximumHostilesPerDungeon * candidateBuildings.Length)
            .ToArray();
        var hostileCreaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = hostileCreatureIds },
            cancellationToken
        );

        var entities = new List<QuestChainCandidateEntity>();
        entities.AddRange(
            candidateBuildings.Select(building => new QuestChainCandidateEntity(
                building.Id,
                building.Name,
                QuestChainEntityTypes.Dungeon,
                DescribeBuilding(building)
            ))
        );
        entities.AddRange(
            hostileCreaturesById.Values.Select(creature => new QuestChainCandidateEntity(
                creature.Id,
                creature.Name,
                QuestChainEntityTypes.Creature,
                DescribeCreature(creature)
            ))
        );

        return entities;
    }

    // ExploreLocation isn't inherently dungeon-only — "reach this place" applies just as well to a
    // notable building within the city. Unlike dungeons, these never carry hostiles, so they only
    // ever populate as QuestChainEntityTypes.Building, never Dungeon (which ClearLocation requires).
    private async Task<IReadOnlyList<QuestChainCandidateEntity>> GatherCityBuildings(
        Guid worldId,
        Guid cityId,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var nonDungeonBuildings = buildings
            .Where(building => !DungeonPopulator.SupportsDungeonType(building.BuildingType))
            .ToArray();
        if (nonDungeonBuildings.Length == 0)
        {
            return [];
        }

        var exteriorLocationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = nonDungeonBuildings.Select(building => building.ExteriorLocationId).ToArray(),
            },
            cancellationToken
        );

        return nonDungeonBuildings
            .Where(building =>
                exteriorLocationsById.TryGetValue(building.ExteriorLocationId, out var location)
                && location.CityId == cityId
            )
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaximumCityBuildings)
            .Select(building => new QuestChainCandidateEntity(
                building.Id,
                building.Name,
                QuestChainEntityTypes.Building,
                DescribeBuilding(building)
            ))
            .ToArray();
    }

    private static string DescribeCreature(Creature creature) =>
        $"level {creature.Level}, {creature.CreatureType}, profession {creature.Profession?.ToString() ?? "none"}; {creature.Biography}";

    private static string DescribeBuilding(Building building) =>
        string.IsNullOrWhiteSpace(building.Premise)
            ? building.Description
            : $"{building.Description} {building.Premise}";
}
