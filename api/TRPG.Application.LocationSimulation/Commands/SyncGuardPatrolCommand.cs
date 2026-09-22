using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GuardPatrols.Queries;
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
    IQueryHandler<ResolveRouteTravelerPositionQuery, RoutePosition?> resolveRouteTravelerPosition,
    IQueryHandler<
        GetGuardPatrolMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getGuardPatrolMembersByRouteTravelerIds,
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

        var travelerIds = travelers.Select(t => t.RouteTravelerId).ToArray();
        var membersByTraveler = await getGuardPatrolMembersByRouteTravelerIds.Handle(
            new GetGuardPatrolMembersByRouteTravelerIdsQuery { RouteTravelerIds = travelerIds },
            cancellationToken
        );
        if (membersByTraveler.Count == 0)
        {
            return;
        }

        var guardCreatureIds = membersByTraveler.Values.SelectMany(ids => ids).Distinct().ToArray();
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = guardCreatureIds },
            cancellationToken
        );

        var relocationsByTargetLocationId = new Dictionary<Guid, List<Guid>>();
        foreach (var traveler in travelers)
        {
            if (!membersByTraveler.TryGetValue(traveler.RouteTravelerId, out var memberIds))
            {
                continue;
            }

            var targetLocationId = await ResolveTargetLocationId(
                traveler,
                command,
                cancellationToken
            );
            if (targetLocationId == null)
            {
                continue;
            }

            AddStragglers(
                relocationsByTargetLocationId,
                targetLocationId.Value,
                memberIds,
                creaturesById
            );
        }

        foreach (var (targetLocationId, creatureIds) in relocationsByTargetLocationId)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = creatureIds,
                    LocationId = targetLocationId,
                },
                cancellationToken
            );
        }
    }

    // A squad always relocates together to wherever its traveler currently resolves to, even when
    // that's a stop other than the one being caught up right now — this is what lets a location
    // catch-up evict a squad that has since moved on, not just pull one in that has just arrived.
    // Neither this codebase nor RouteCycle models mid-leg travel time visually (CreatureJob-driven
    // NPCs teleport between schedule points the same way), so "in transit" resolves to the
    // destination stop rather than leaving the squad stranded at the stop it already departed.
    private async Task<Guid?> ResolveTargetLocationId(
        RouteTravelerSummary traveler,
        SyncGuardPatrolCommand command,
        CancellationToken cancellationToken
    )
    {
        var position = await resolveRouteTravelerPosition.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = traveler.RouteTravelerId,
                Playtime = command.Playtime,
                SpeedUnitsPerHour = countryPatrolOptions.Value.SpeedUnitsPerHour,
            },
            cancellationToken
        );

        return position switch
        {
            RoutePosition.Lingering lingering => lingering.LocationId,
            RoutePosition.InTransit inTransit => inTransit.ToLocationId,
            _ => null,
        };
    }

    private static void AddStragglers(
        Dictionary<Guid, List<Guid>> relocationsByTargetLocationId,
        Guid targetLocationId,
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

            if (creature.State == CreatureState.Dead || creature.LocationId == targetLocationId)
            {
                continue;
            }

            relocationsByTargetLocationId.TryAdd(targetLocationId, []);
            relocationsByTargetLocationId[targetLocationId].Add(creatureId);
        }
    }
}
