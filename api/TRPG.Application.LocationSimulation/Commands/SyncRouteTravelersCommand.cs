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
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartWalkingCommand> startWalking,
    ICommandHandler<SetCreatureActivityCommand> setCreatureActivity
) : ICommandHandler<SyncRouteTravelersCommand>
{
    public async Task Handle(
        SyncRouteTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var travelers = await GetCreatureTravelers(command, cancellationToken);
        if (travelers.MemberIdsByTravelerId.Count == 0)
        {
            return;
        }

        var syncData = await GetSyncData(command, travelers, cancellationToken);
        var relocations = BuildRelocations(travelers, syncData);
        await PersistRelocations(relocations, cancellationToken);
    }

    private async Task<CreatureTravelers> GetCreatureTravelers(
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
            return new CreatureTravelers(
                new Dictionary<Guid, RouteTravelerSummary>(),
                new Dictionary<Guid, IReadOnlyList<Guid>>()
            );
        }

        var memberIdsByTravelerId = await getRouteTravelerMembersByRouteTravelerIds.Handle(
            new GetRouteTravelerMembersByRouteTravelerIdsQuery
            {
                RouteTravelerIds = routeTravelers
                    .Select(traveler => traveler.RouteTravelerId)
                    .ToArray(),
            },
            cancellationToken
        );
        return new CreatureTravelers(
            routeTravelers.ToDictionary(traveler => traveler.RouteTravelerId),
            memberIdsByTravelerId
        );
    }

    private async Task<RouteTravelerSyncData> GetSyncData(
        SyncRouteTravelersCommand command,
        CreatureTravelers travelers,
        CancellationToken cancellationToken
    )
    {
        var positionsByTravelerId = await resolveRouteTravelerPositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = travelers.MemberIdsByTravelerId.Keys.ToArray(),
                GameTime = command.GameTime,
            },
            cancellationToken
        );
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = travelers
                    .MemberIdsByTravelerId.Values.SelectMany(ids => ids)
                    .Distinct()
                    .ToArray(),
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
                    ArrivedAt = target.ArrivedAt,
                },
                cancellationToken
            );
            await ApplyPhase(target, creatureIds, cancellationToken);
        }
    }

    private async Task ApplyPhase(
        RouteTravelerTarget target,
        IReadOnlyCollection<Guid> creatureIds,
        CancellationToken cancellationToken
    )
    {
        switch (target.Phase)
        {
            case RouteTravelerPhase.Walking:
                await startWalking.Handle(
                    new StartWalkingCommand { CreatureIds = creatureIds, Exit = target.Exit },
                    cancellationToken
                );
                break;
            case RouteTravelerPhase.Arrived:
                await setCreatureActivity.Handle(
                    new SetCreatureActivityCommand
                    {
                        CreatureIds = creatureIds,
                        Activity = target.ArrivalActivity,
                    },
                    cancellationToken
                );
                break;
        }
    }

    private static IReadOnlyDictionary<RouteTravelerTarget, List<Guid>> BuildRelocations(
        CreatureTravelers travelers,
        RouteTravelerSyncData syncData
    )
    {
        var relocations = new Dictionary<RouteTravelerTarget, List<Guid>>();
        foreach (var (routeTravelerId, memberIds) in travelers.MemberIdsByTravelerId)
        {
            if (!syncData.PositionsByTravelerId.TryGetValue(routeTravelerId, out var position))
            {
                continue;
            }

            var arrivalActivity = travelers.TravelersById[routeTravelerId].ArrivalActivity;
            foreach (var memberId in memberIds)
            {
                if (syncData.CreaturesById.TryGetValue(memberId, out var creature))
                {
                    AddRelocation(relocations, position.Position, arrivalActivity, creature);
                }
            }
        }
        return relocations;
    }

    // Movement changes only on a transition: leaving a stop (walking) or arriving at one (the arrival activity).
    private static void AddRelocation(
        Dictionary<RouteTravelerTarget, List<Guid>> relocations,
        RouteTimelinePosition position,
        CreatureActivity? arrivalActivity,
        Creature creature
    )
    {
        if (creature.IsEngaged || creature.Condition == CreatureCondition.Dead)
        {
            return;
        }

        var target = ResolveTarget(position, arrivalActivity, creature);
        if (target == null)
        {
            return;
        }

        relocations.TryAdd(target, []);
        relocations[target].Add(creature.Id);
    }

    private static RouteTravelerTarget? ResolveTarget(
        RouteTimelinePosition position,
        CreatureActivity? arrivalActivity,
        Creature creature
    ) =>
        position switch
        {
            RouteTimelinePosition.Pending pending => creature.LocationId == pending.LocationId
                ? null
                : new RouteTravelerTarget(
                    pending.LocationId,
                    RouteTravelerPhase.Waiting,
                    null,
                    null
                ),
            RouteTimelinePosition.Lingering lingering => ArriveAt(
                lingering.LocationId,
                lingering.ArrivedAtGameTime,
                arrivalActivity,
                creature
            ),
            RouteTimelinePosition.InTransit inTransit => creature.LocationId
                == inTransit.FromLocationId
            && creature.Movement == CreatureMovement.Walking
                ? null
                : new RouteTravelerTarget(
                    inTransit.FromLocationId,
                    RouteTravelerPhase.Walking,
                    null,
                    null,
                    WalkExit.From(inTransit)
                ),
            _ => throw new InvalidOperationException("Unknown route position."),
        };

    private static RouteTravelerTarget? ArriveAt(
        Guid locationId,
        GameInstant arrivedAt,
        CreatureActivity? arrivalActivity,
        Creature creature
    ) =>
        creature.LocationId == locationId && creature.Movement != CreatureMovement.Walking
            ? null
            : new RouteTravelerTarget(
                locationId,
                RouteTravelerPhase.Arrived,
                arrivalActivity,
                arrivedAt
            );

    private record CreatureTravelers(
        IReadOnlyDictionary<Guid, RouteTravelerSummary> TravelersById,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> MemberIdsByTravelerId
    );

    private record RouteTravelerSyncData(
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition> PositionsByTravelerId,
        IReadOnlyDictionary<Guid, Creature> CreaturesById
    );

    private enum RouteTravelerPhase
    {
        Waiting,
        Walking,
        Arrived,
    }

    private record RouteTravelerTarget(
        Guid LocationId,
        RouteTravelerPhase Phase,
        CreatureActivity? ArrivalActivity,
        GameInstant? ArrivedAt,
        WalkExit? Exit = null
    );
}
