using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class ActivateTriggerCommand
{
    public required Guid TriggerId { get; init; }
}

public record ActivateTriggerResult(bool AlreadyActivated);

internal class ActivateTriggerCommandHandler(IPropsDbContext context)
    : ICommandHandler<ActivateTriggerCommand, ActivateTriggerResult>
{
    public async Task<ActivateTriggerResult> Handle(
        ActivateTriggerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var trigger =
            await context
                .Props.OfType<Trigger>()
                .FirstOrDefaultAsync(t => t.Id == command.TriggerId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Trigger), command.TriggerId);

        if (trigger.IsActivated)
        {
            return new ActivateTriggerResult(AlreadyActivated: true);
        }

        trigger.IsActivated = true;
        await context.SaveChangesAsync(cancellationToken);

        return new ActivateTriggerResult(AlreadyActivated: false);
    }
}
