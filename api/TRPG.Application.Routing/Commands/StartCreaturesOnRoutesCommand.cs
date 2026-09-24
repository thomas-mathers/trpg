using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public record StartCreatureOnRouteRequest(
    Guid CreatureId,
    Guid RouteId,
    TimeSpan StartedAtPlaytime,
    string Purpose
);

public class StartCreaturesOnRoutesCommand
{
    public required IReadOnlyCollection<StartCreatureOnRouteRequest> Routes { get; init; }
}

internal class StartCreaturesOnRoutesCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds
) : ICommandHandler<StartCreaturesOnRoutesCommand>
{
    public async Task Handle(
        StartCreaturesOnRoutesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Routes.Count == 0)
        {
            return;
        }

        ValidateRequests(command.Routes);
        var creatureIds = command.Routes.Select(request => request.CreatureId).ToArray();
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        var missingCreatureId = creatureIds.FirstOrDefault(id => !creatures.ContainsKey(id));
        if (missingCreatureId != Guid.Empty)
        {
            throw new EntityNotFoundException(nameof(Creature), missingCreatureId);
        }

        var routeIds = command.Routes.Select(request => request.RouteId).Distinct().ToArray();
        var routes = await context
            .Routes.AsNoTracking()
            .Where(route => routeIds.AsEnumerable().Contains(route.Id))
            .ToDictionaryAsync(route => route.Id, cancellationToken);
        var missingRouteId = routeIds.FirstOrDefault(id => !routes.ContainsKey(id));
        if (missingRouteId != Guid.Empty)
        {
            throw new EntityNotFoundException(nameof(Route), missingRouteId);
        }
        if (routes.Values.Any(route => route.Traversal != RouteTraversal.Cyclic))
        {
            throw new InvalidOperationException("A scheduled route activity must be cyclic.");
        }

        var routeSteps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => routeIds.AsEnumerable().Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var firstLocationByRouteId = routeSteps
            .GroupBy(step => step.RouteId)
            .ToDictionary(group => group.Key, group => group.First().LocationId);
        foreach (var request in command.Routes)
        {
            if (creatures[request.CreatureId].LocationId != firstLocationByRouteId[request.RouteId])
            {
                throw new InvalidOperationException(
                    "A creature must be at the first route step before starting the route."
                );
            }
        }

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await CreatureRouteCleaner.Remove(context, creatureIds, cancellationToken);
        foreach (var request in command.Routes)
        {
            var creature = creatures[request.CreatureId];
            var traveler = new RouteTraveler
            {
                WorldId = creature.WorldId,
                RouteId = request.RouteId,
                StartedAtPlaytime = request.StartedAtPlaytime,
                SpeedUnitsPerHour = creature.MovementSpeed,
                Purpose = request.Purpose,
            };
            context.RouteTravelers.Add(traveler);
            context.RouteTravelerMembers.Add(
                new RouteTravelerMember
                {
                    WorldId = creature.WorldId,
                    RouteTravelerId = traveler.Id,
                    CreatureId = creature.Id,
                }
            );
        }
        await context.SaveChangesAsync(cancellationToken);
        transaction.Complete();
    }

    private static void ValidateRequests(IReadOnlyCollection<StartCreatureOnRouteRequest> requests)
    {
        if (requests.Select(request => request.CreatureId).Distinct().Count() != requests.Count)
        {
            throw new ArgumentException(
                "A creature can only start one route at a time.",
                nameof(requests)
            );
        }
        if (requests.Any(request => string.IsNullOrWhiteSpace(request.Purpose)))
        {
            throw new ArgumentException("A route purpose is required.", nameof(requests));
        }
    }
}
