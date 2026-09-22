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
        var lingeringTravelerIds = await ResolveLingeringPatrolTravelerIds(
            command,
            cancellationToken
        );
        if (lingeringTravelerIds.Count == 0)
        {
            return;
        }

        var membersByTraveler = await getGuardPatrolMembersByRouteTravelerIds.Handle(
            new GetGuardPatrolMembersByRouteTravelerIdsQuery
            {
                RouteTravelerIds = lingeringTravelerIds,
            },
            cancellationToken
        );
        var guardCreatureIds = membersByTraveler.Values.SelectMany(ids => ids).Distinct().ToArray();
        if (guardCreatureIds.Length == 0)
        {
            return;
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = guardCreatureIds },
            cancellationToken
        );
        var toRelocate = creaturesById
            .Values.Where(creature =>
                creature.State != CreatureState.Dead && creature.LocationId != command.LocationId
            )
            .Select(creature => creature.Id)
            .ToArray();
        if (toRelocate.Length == 0)
        {
            return;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = toRelocate,
                LocationId = command.LocationId,
            },
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<Guid>> ResolveLingeringPatrolTravelerIds(
        SyncGuardPatrolCommand command,
        CancellationToken cancellationToken
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
            return [];
        }

        var lingeringTravelerIds = new List<Guid>();
        foreach (var traveler in travelers)
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
            if (
                position is RoutePosition.Lingering atThisStop
                && atThisStop.LocationId == command.LocationId
            )
            {
                lingeringTravelerIds.Add(traveler.RouteTravelerId);
            }
        }

        return lingeringTravelerIds;
    }
}
