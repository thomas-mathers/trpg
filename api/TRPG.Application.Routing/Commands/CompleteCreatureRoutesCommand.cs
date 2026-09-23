using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Commands;

public class CompleteCreatureRoutesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class CompleteCreatureRoutesCommandHandler(IRoutingDbContext context)
    : ICommandHandler<CompleteCreatureRoutesCommand>
{
    public Task Handle(
        CompleteCreatureRoutesCommand command,
        CancellationToken cancellationToken = default
    ) => CreatureRouteCleaner.Remove(context, command.CreatureIds, cancellationToken);
}

internal static class CreatureRouteCleaner
{
    public static async Task Remove(
        IRoutingDbContext context,
        IReadOnlyCollection<Guid> creatureIds,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0)
        {
            return;
        }

        var travelerIds = await context
            .RouteTravelerMembers.Where(member =>
                creatureIds.AsEnumerable().Contains(member.CreatureId)
            )
            .Select(member => member.RouteTravelerId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (travelerIds.Length == 0)
        {
            return;
        }

        await context
            .RouteTravelerMembers.Where(member =>
                creatureIds.AsEnumerable().Contains(member.CreatureId)
            )
            .ExecuteDeleteAsync(cancellationToken);
        await context
            .RouteTravelers.Where(traveler =>
                travelerIds.AsEnumerable().Contains(traveler.Id)
                && !context.RouteTravelerMembers.Any(member =>
                    member.RouteTravelerId == traveler.Id
                )
            )
            .ExecuteDeleteAsync(cancellationToken);
    }
}
