using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public class StartJourneysCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class StartJourneysCommandHandler(IRoutingDbContext context)
    : ICommandHandler<StartJourneysCommand>
{
    public async Task Handle(
        StartJourneysCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        var journeyIds = await context
            .JourneyMembers.Where(member =>
                command.CreatureIds.AsEnumerable().Contains(member.CreatureId)
            )
            .Select(member => member.JourneyId)
            .ToArrayAsync(cancellationToken);
        await context
            .Journeys.Where(journey =>
                journeyIds.AsEnumerable().Contains(journey.Id)
                && journey.Status == JourneyStatus.Planned
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(journey => journey.Status, JourneyStatus.Traveling),
                cancellationToken
            );
        await context.SaveChangesAsync(cancellationToken);
    }
}
