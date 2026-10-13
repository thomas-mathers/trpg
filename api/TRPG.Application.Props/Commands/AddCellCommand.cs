using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class AddCellCommand
{
    public required Cell Cell { get; init; }
}

internal class AddCellCommandHandler(
    IPropsDbContext context,
    IMemoryCache cache,
    IDomainEventPublisher<PropAddedEvent> propAdded
) : ICommandHandler<AddCellCommand>
{
    public async Task Handle(AddCellCommand command, CancellationToken cancellationToken = default)
    {
        context.Props.Add(command.Cell);
        await context.SaveChangesAsync(cancellationToken);

        cache.Remove(GetPropsByLocationIdQueryHandler.CacheKey(command.Cell.LocationId));
        await propAdded.Publish(
            new PropAddedEvent(command.Cell.WorldId, command.Cell.LocationId),
            cancellationToken
        );
    }
}
