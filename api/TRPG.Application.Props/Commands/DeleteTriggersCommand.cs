using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class DeleteTriggersCommand
{
    public required IReadOnlyCollection<Guid> TriggerIds { get; init; }
}

internal class DeleteTriggersCommandHandler(IPropsDbContext context)
    : ICommandHandler<DeleteTriggersCommand>
{
    public async Task Handle(
        DeleteTriggersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.TriggerIds.Count == 0)
        {
            return;
        }

        await context
            .Props.OfType<Trigger>()
            .Where(trigger => command.TriggerIds.AsEnumerable().Contains(trigger.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
