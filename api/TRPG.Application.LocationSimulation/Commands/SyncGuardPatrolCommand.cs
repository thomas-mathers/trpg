using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncGuardPatrolCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class SyncGuardPatrolCommandHandler(
    IQueryHandler<
        GetRouteTravelersByLocationIdQuery,
        IReadOnlyList<RouteTravelerSummary>
    > getRouteTravelersByLocationId,
    IQueryHandler<
        ResolveRouteTravelerPositionsQuery,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>
    > resolveRouteTravelerPositions,
    IQueryHandler<
        GetRouteTravelerMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getRouteTravelerMembersByRouteTravelerIds,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IOptionsSnapshot<CountryPatrolOptions> countryPatrolOptions
) : ICommandHandler<SyncGuardPatrolCommand>
{
    public async Task Handle(
        SyncGuardPatrolCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var travelers = await getRouteTravelersByLocationId.Handle(
            new GetRouteTravelersByLocationIdQuery
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );
        if (travelers.Count == 0)
        {
            return;
        }

        var travelerIds = travelers
            .Where(t => t.Kind == RouteTravelerKind.GuardPatrol)
            .Select(t => t.RouteTravelerId)
            .ToArray();
        var membersByTraveler = await getRouteTravelerMembersByRouteTravelerIds.Handle(
            new GetRouteTravelerMembersByRouteTravelerIdsQuery { RouteTravelerIds = travelerIds },
            cancellationToken
        );
        if (membersByTraveler.Count == 0)
        {
            return;
        }

        var positionsByTravelerId = await resolveRouteTravelerPositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = membersByTraveler.Keys.ToArray(),
                Playtime = command.Playtime,
                SpeedUnitsPerHour = countryPatrolOptions.Value.SpeedUnitsPerHour,
            },
            cancellationToken
        );

        var guardCreatureIds = membersByTraveler.Values.SelectMany(ids => ids).Distinct().ToArray();
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = guardCreatureIds },
            cancellationToken
        );

        var relocationsByTarget = new Dictionary<GuardPatrolTarget, List<Guid>>();
        foreach (var traveler in travelers)
        {
            if (!membersByTraveler.TryGetValue(traveler.RouteTravelerId, out var memberIds))
            {
                continue;
            }

            var target = ResolveTarget(
                positionsByTravelerId.GetValueOrDefault(traveler.RouteTravelerId)?.Position
            );
            if (target == null)
            {
                continue;
            }

            AddStragglers(relocationsByTarget, target, memberIds, creaturesById);
        }

        foreach (var (target, creatureIds) in relocationsByTarget)
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

    // A squad always relocates together to wherever its traveler currently resolves to, even when
    // that's a stop other than the one being caught up right now — this is what lets a location
    // catch-up evict a squad that has since moved on, not just pull one in that has just arrived.
    // Patrol routes have no linger time, so a traveler is effectively always InTransit — this
    // attributes it to the stop it just departed (not the destination), so State.Patrolling and the
    // location never contradict each other ("marching along the road" said of a squad already shown
    // standing at the far end would be a lie). A nonzero-linger route (none exist yet, but the math
    // still supports it) still resolves Lingering to a real stop with State.Idle.
    private static GuardPatrolTarget? ResolveTarget(RoutePosition? position) =>
        position switch
        {
            RoutePosition.Lingering lingering => new GuardPatrolTarget(
                lingering.LocationId,
                CreatureState.Idle
            ),
            RoutePosition.InTransit inTransit => new GuardPatrolTarget(
                inTransit.FromLocationId,
                CreatureState.Patrolling
            ),
            _ => null,
        };

    private static void AddStragglers(
        Dictionary<GuardPatrolTarget, List<Guid>> relocationsByTarget,
        GuardPatrolTarget target,
        IReadOnlyList<Guid> memberIds,
        IReadOnlyDictionary<Guid, Creature> creaturesById
    )
    {
        foreach (var creatureId in memberIds)
        {
            if (!creaturesById.TryGetValue(creatureId, out var creature))
            {
                continue;
            }

            if (creature.State == CreatureState.Dead)
            {
                continue;
            }

            if (creature.LocationId == target.LocationId && creature.State == target.State)
            {
                continue;
            }

            relocationsByTarget.TryAdd(target, []);
            relocationsByTarget[target].Add(creatureId);
        }
    }

    private record GuardPatrolTarget(Guid LocationId, CreatureState State);
}
