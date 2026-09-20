using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
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

    // Manual-testing hook only: forces the casting pass to a specific giver faction. See
    // QuestChainGeneratorInput.ForcedGiverFactionId.
    public Guid? ForcedGiverFactionId { get; init; }
}

// Finds someone at the seeding location willing to point the player toward a bigger story, then
// gathers a bounded pool of real entities (giver candidates, nearby dungeon buildings and their
// hostiles, and notable non-dungeon city buildings) and enqueues the slow LLM-authored chain
// generation as a background job — never calls the LLM inline, since a single generation call has
// been measured at 50-70 seconds. No-ops at any step where nothing eligible exists, same contract
// as the other Seed*QuestCommand types.
internal class SeedLlmQuestChainCommandHandler(
    ILocationSimulationDbContext context,
    IFactionsDbContext factionsContext,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<GetLocationIdsByStateIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByStateId,
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
    IQuestChainGenerationScheduler scheduler,
    IOptionsSnapshot<QuestChainSeedingOptions> optionsSnapshot
) : ICommandHandler<SeedLlmQuestChainCommand, bool>
{
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
        entities.AddRange(
            await GatherWildernessEntities(
                command.WorldId,
                entranceLocation.StateId,
                cancellationToken
            )
        );
        entities = entities.DistinctBy(entity => entity.Id).ToList();
        entities = (
            await AttachFactionBindings(command.WorldId, entities, cancellationToken)
        ).ToList();

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

        // Both ends scale with level, from a short, tight early-game range up toward
        // ChainLengthCeiling — a 45-61 node budget (uncapped level-30 math from an earlier pass)
        // exhausted every block-graph retry and never completed, so nothing may ever exceed the
        // ceiling regardless of how much level scaling would otherwise add.
        var options = optionsSnapshot.Value;
        var levelBonus = options.ChainLengthPerLevel * (command.PlayerLevel - 1);
        var maximumChainLength = Math.Min(
            options.MaximumChainLengthBase + levelBonus,
            options.ChainLengthCeiling
        );
        var minimumChainLength = Math.Min(
            options.MinimumChainLengthBase + levelBonus,
            maximumChainLength
        );
        var factionStandings = await factionsContext
            .FactionStandings.AsNoTracking()
            .Where(standing => standing.WorldId == command.WorldId && standing.Score < 0)
            .Select(standing => new QuestChainFactionStanding(
                standing.FactionId,
                standing.OtherFactionId,
                standing.Score
            ))
            .ToArrayAsync(cancellationToken);
        await scheduler.ScheduleAsync(
            new GenerateQuestChainCommand
            {
                RequestId = request.Id,
                ChainPremise =
                    "A new multi-step story is unfolding, drawing on the people and dangers in and around this city.",
                MinimumChainLength = minimumChainLength,
                MaximumChainLength = maximumChainLength,
                AvailableEntities = entities,
                FactionStandings = factionStandings,
                ForcedGiverFactionId = command.ForcedGiverFactionId,
            },
            cancellationToken
        );

        return true;
    }

    private async Task<IReadOnlyList<QuestChainCandidateEntity>> AttachFactionBindings(
        Guid worldId,
        IReadOnlyCollection<QuestChainCandidateEntity> entities,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = entities
            .Where(entity => entity.Type == QuestChainEntityTypes.Creature)
            .Select(entity => entity.Id)
            .ToArray();
        var factions = await factionsContext
            .Factions.AsNoTracking()
            .Where(faction => faction.WorldId == worldId)
            .ToDictionaryAsync(faction => faction.Id, cancellationToken);
        var factionIdsByCreatureId = await factionsContext
            .FactionMembers.AsNoTracking()
            .Where(member => creatureIds.AsEnumerable().Contains(member.CreatureId))
            .GroupBy(member => member.CreatureId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(member => member.FactionId).ToArray(),
                cancellationToken
            );

        return entities
            .Select(entity =>
            {
                Faction? faction =
                    entity.FactionId is { } entityFactionId
                    && factions.TryGetValue(entityFactionId, out var boundFaction)
                        ? boundFaction
                        : null;
                if (
                    faction == null
                    && factionIdsByCreatureId.TryGetValue(entity.Id, out var factionIds)
                    && factionIds
                        .Select(id => factions[id])
                        .OrderBy(f => FactionPriority(f.Kind))
                        .FirstOrDefault()
                        is { } creatureFaction
                )
                {
                    faction = creatureFaction;
                }

                return faction == null
                    ? entity
                    : entity with
                    {
                        FactionId = faction.Id,
                        FactionName = faction.Name,
                        FactionKind = faction.Kind,
                    };
            })
            .ToArray();
    }

    private static int FactionPriority(FactionKind kind) =>
        kind switch
        {
            FactionKind.Joinable or FactionKind.Antagonist => 0,
            FactionKind.CityGuard or FactionKind.Castle => 1,
            FactionKind.Wilderness => 2,
            _ => 3,
        };

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
        var priorityFactionIds = await factionsContext
            .Factions.AsNoTracking()
            .Where(faction =>
                faction.Kind == FactionKind.Joinable
                || faction.Kind == FactionKind.Antagonist
                || (
                    faction.CityId == cityId
                    && (faction.Kind == FactionKind.CityGuard || faction.Kind == FactionKind.Castle)
                )
            )
            .Select(faction => faction.Id)
            .ToArrayAsync(cancellationToken);
        var priorityCreatureIds = await factionsContext
            .FactionMembers.AsNoTracking()
            .Where(member => priorityFactionIds.AsEnumerable().Contains(member.FactionId))
            .Select(member => member.CreatureId)
            .ToHashSetAsync(cancellationToken);

        return candidates
            .Values.Where(creature => CreatureTypes.Humanoid.Contains(creature.CreatureType))
            .OrderBy(creature => priorityCreatureIds.Contains(creature.Id) ? 0 : 1)
            .ThenBy(_ => Random.Shared.Next())
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
            .OrderBy(building => building.FactionId == null ? 1 : 0)
            .ThenBy(_ => Random.Shared.Next())
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
                DescribeBuilding(building),
                building.FactionId
            ))
        );
        entities.AddRange(
            hostileCreaturesById.Values.Select(creature => new QuestChainCandidateEntity(
                creature.Id,
                creature.Name,
                QuestChainEntityTypes.Creature,
                DescribeCreature(creature),
                CanGiveQuests: false
            ))
        );

        return entities;
    }

    private async Task<IReadOnlyList<QuestChainCandidateEntity>> GatherWildernessEntities(
        Guid worldId,
        Guid stateId,
        CancellationToken cancellationToken
    )
    {
        var locationIds = await getLocationIdsByStateId.Handle(
            new GetLocationIdsByStateIdQuery { StateId = stateId },
            cancellationToken
        );
        var hostilesByLocation = await getLivingHostileCreatureIdsByLocation.Handle(
            new GetLivingHostileCreatureIdsByLocationQuery
            {
                WorldId = worldId,
                LocationIds = locationIds,
            },
            cancellationToken
        );
        var hostileIds = hostilesByLocation
            .Values.SelectMany(ids => ids)
            .Distinct()
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaximumHostilesPerDungeon)
            .ToArray();
        var hostiles = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = hostileIds },
            cancellationToken
        );

        return hostiles
            .Values.Select(creature => new QuestChainCandidateEntity(
                creature.Id,
                creature.Name,
                QuestChainEntityTypes.Creature,
                DescribeCreature(creature),
                CanGiveQuests: false
            ))
            .ToArray();
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
