using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.EventHandlers;

internal sealed class CreaturesEngagedEventHandler(IRoutingDbContext context)
    : IDomainEventConsumer<CreaturesEngagedEvent>
{
    public async Task Handle(
        CreaturesEngagedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var travelerIds = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => domainEvent.CreatureIds.Contains(member.CreatureId))
            .Select(member => member.RouteTravelerId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (travelerIds.Length == 0)
        {
            return;
        }

        await context
            .RouteTravelers.Where(traveler =>
                travelerIds.AsEnumerable().Contains(traveler.Id)
                && traveler.PausedAtGameTime == null
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        traveler => traveler.PausedAtGameTime,
                        domainEvent.GameTime
                    ),
                cancellationToken
            );
    }
}
