using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class MarkTrapResolvedCommand
{
    public required Guid TriggerId { get; init; }
}

internal class MarkTrapResolvedCommandHandler(IPropsDbContext context)
    : ICommandHandler<MarkTrapResolvedCommand>
{
    public async Task Handle(
        MarkTrapResolvedCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Trigger>()
            .Where(t => t.Id == command.TriggerId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsResolved, true), cancellationToken);
}
