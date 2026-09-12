using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class AddCellCommand
{
    public required Cell Cell { get; init; }
}

internal class AddCellCommandHandler(IPropsDbContext context) : ICommandHandler<AddCellCommand>
{
    public async Task Handle(AddCellCommand command, CancellationToken cancellationToken = default)
    {
        context.Props.Add(command.Cell);
        await context.SaveChangesAsync(cancellationToken);
    }
}
