using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class AddTriggersCommand
{
    public required IReadOnlyCollection<Trigger> Triggers { get; init; }
}

internal class AddTriggersCommandHandler(IPropsDbContext context)
    : ICommandHandler<AddTriggersCommand>
{
    public async Task Handle(
        AddTriggersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Props.AddRange(command.Triggers);
        await context.SaveChangesAsync(cancellationToken);
    }
}
