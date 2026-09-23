using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncRoadTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class SyncRoadTravelersCommandHandler(
    IQueryHandler<
        GetRouteTravelersByLocationIdQuery,
        IReadOnlyList<RouteTravelerSummary>
    > getRouteTravelersByLocationId,
    IQueryHandler<
        GetRouteTravelerMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getRouteTravelerMembersByRouteTravelerIds,
    IQueryHandler<
        ResolveRouteTravelerPositionsQuery,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>
    > resolveRouteTravelerPositions,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
) : ICommandHandler<SyncRoadTravelersCommand>
{
    public async Task Handle(
        SyncRoadTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var roadTravelers = await GetRoadTravelers(command, cancellationToken);
        if (roadTravelers.Count == 0)
        {
            return;
        }

        var syncData = await GetSyncData(command, roadTravelers, cancellationToken);
        var relocations = BuildRelocations(
            roadTravelers,
            syncData.PositionsByTravelerId,
            syncData.CreaturesById
        );
        await PersistRelocations(relocations, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetRoadTravelers(
        SyncRoadTravelersCommand command,
        CancellationToken cancellationToken
    )
    {
        var routeTravelers = await getRouteTravelersByLocationId.Handle(
            new GetRouteTravelersByLocationIdQuery
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );
        if (routeTravelers.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var travelerIds = routeTravelers
            .Where(t => t.Kind is RouteTravelerKind.Pilgrim or RouteTravelerKind.Adventurer)
            .Select(t => t.RouteTravelerId)
            .ToArray();
        return await getRouteTravelerMembersByRouteTravelerIds.Handle(
            new GetRouteTravelerMembersByRouteTravelerIdsQuery { RouteTravelerIds = travelerIds },
            cancellationToken
        );
    }

    private async Task<RoadTravelerSyncData> GetSyncData(
        SyncRoadTravelersCommand command,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> roadTravelers,
        CancellationToken cancellationToken
    )
    {
        var positionsByTravelerId = await resolveRouteTravelerPositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = roadTravelers.Keys.ToArray(),
                Playtime = command.Playtime,
            },
            cancellationToken
        );
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = roadTravelers.Values.SelectMany(ids => ids).Distinct().ToArray(),
            },
            cancellationToken
        );
        return new RoadTravelerSyncData(positionsByTravelerId, creaturesById);
    }

    private async Task PersistRelocations(
        IReadOnlyDictionary<RoadTravelerTarget, List<Guid>> relocations,
        CancellationToken cancellationToken
    )
    {
        foreach (var (target, creatureIds) in relocations)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = creatureIds,
                    LocationId = target.LocationId,
                    State = target.State,
                },
                cancellationToken
            );
        }
    }

    private static IReadOnlyDictionary<RoadTravelerTarget, List<Guid>> BuildRelocations(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> roadTravelersByRouteTravelerId,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition> positionsByTravelerId,
        IReadOnlyDictionary<Guid, Creature> creaturesById
    )
    {
        var relocations = new Dictionary<RoadTravelerTarget, List<Guid>>();
        foreach (var (routeTravelerId, memberIds) in roadTravelersByRouteTravelerId)
        {
            if (positionsByTravelerId.TryGetValue(routeTravelerId, out var position))
            {
                foreach (var memberId in memberIds)
                {
                    if (creaturesById.TryGetValue(memberId, out var creature))
                    {
                        AddRelocation(relocations, ResolveTarget(position.Position), creature);
                    }
                }
            }
        }
        return relocations;
    }

    private static void AddRelocation(
        Dictionary<RoadTravelerTarget, List<Guid>> relocations,
        RoadTravelerTarget target,
        Creature creature
    )
    {
        if (
            creature.State == CreatureState.Dead
            || (creature.LocationId == target.LocationId && creature.State == target.State)
        )
        {
            return;
        }

        relocations.TryAdd(target, []);
        relocations[target].Add(creature.Id);
    }

    private static RoadTravelerTarget ResolveTarget(RouteTimelinePosition position) =>
        position switch
        {
            RouteTimelinePosition.Pending pending => new RoadTravelerTarget(
                pending.LocationId,
                CreatureState.Idle
            ),
            RouteTimelinePosition.Lingering lingering => new RoadTravelerTarget(
                lingering.LocationId,
                CreatureState.Idle
            ),
            RouteTimelinePosition.InTransit inTransit => new RoadTravelerTarget(
                inTransit.FromLocationId,
                CreatureState.Walking
            ),
            RouteTimelinePosition.Arrived arrived => new RoadTravelerTarget(
                arrived.LocationId,
                CreatureState.Idle
            ),
            _ => throw new InvalidOperationException("Unknown route position."),
        };

    private record RoadTravelerSyncData(
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition> PositionsByTravelerId,
        IReadOnlyDictionary<Guid, Creature> CreaturesById
    );

    private record RoadTravelerTarget(Guid LocationId, CreatureState State);
}
