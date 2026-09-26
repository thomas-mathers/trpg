using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncRouteTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncRouteTravelersCommandHandler(
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
) : ICommandHandler<SyncRouteTravelersCommand>
{
    public async Task Handle(
        SyncRouteTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var travelers = await GetCreatureTravelers(command, cancellationToken);
        if (travelers.Count == 0)
        {
            return;
        }

        var syncData = await GetSyncData(command, travelers, cancellationToken);
        var relocations = BuildRelocations(
            travelers,
            syncData.PositionsByTravelerId,
            syncData.CreaturesById
        );
        await PersistRelocations(relocations, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCreatureTravelers(
        SyncRouteTravelersCommand command,
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

        return await getRouteTravelerMembersByRouteTravelerIds.Handle(
            new GetRouteTravelerMembersByRouteTravelerIdsQuery
            {
                RouteTravelerIds = routeTravelers
                    .Select(traveler => traveler.RouteTravelerId)
                    .ToArray(),
            },
            cancellationToken
        );
    }

    private async Task<RouteTravelerSyncData> GetSyncData(
        SyncRouteTravelersCommand command,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> travelers,
        CancellationToken cancellationToken
    )
    {
        var positionsByTravelerId = await resolveRouteTravelerPositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = travelers.Keys.ToArray(),
                GameTime = command.GameTime,
            },
            cancellationToken
        );
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = travelers.Values.SelectMany(ids => ids).Distinct().ToArray(),
            },
            cancellationToken
        );
        return new RouteTravelerSyncData(positionsByTravelerId, creaturesById);
    }

    private async Task PersistRelocations(
        IReadOnlyDictionary<RouteTravelerTarget, List<Guid>> relocations,
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

    private static IReadOnlyDictionary<RouteTravelerTarget, List<Guid>> BuildRelocations(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> travelersByRouteTravelerId,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition> positionsByTravelerId,
        IReadOnlyDictionary<Guid, Creature> creaturesById
    )
    {
        var relocations = new Dictionary<RouteTravelerTarget, List<Guid>>();
        foreach (var (routeTravelerId, memberIds) in travelersByRouteTravelerId)
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
        Dictionary<RouteTravelerTarget, List<Guid>> relocations,
        RouteTravelerTarget target,
        Creature creature
    )
    {
        if (
            creature.IsEngaged
            || creature.State == CreatureState.Dead
            || (creature.LocationId == target.LocationId && creature.State == target.State)
        )
        {
            return;
        }

        relocations.TryAdd(target, []);
        relocations[target].Add(creature.Id);
    }

    private static RouteTravelerTarget ResolveTarget(RouteTimelinePosition position) =>
        position switch
        {
            RouteTimelinePosition.Pending pending => new RouteTravelerTarget(
                pending.LocationId,
                CreatureState.Idle
            ),
            RouteTimelinePosition.Lingering lingering => new RouteTravelerTarget(
                lingering.LocationId,
                CreatureState.Idle
            ),
            RouteTimelinePosition.InTransit inTransit => new RouteTravelerTarget(
                inTransit.FromLocationId,
                CreatureState.Walking
            ),
            RouteTimelinePosition.Arrived arrived => new RouteTravelerTarget(
                arrived.LocationId,
                CreatureState.Idle
            ),
            _ => throw new InvalidOperationException("Unknown route position."),
        };

    private record RouteTravelerSyncData(
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition> PositionsByTravelerId,
        IReadOnlyDictionary<Guid, Creature> CreaturesById
    );

    private record RouteTravelerTarget(Guid LocationId, CreatureState State);
}
