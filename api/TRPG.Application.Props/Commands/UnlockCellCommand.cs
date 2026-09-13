using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class UnlockCellCommand
{
    public required Guid CellId { get; init; }
}

internal class UnlockCellCommandHandler(IPropsDbContext context)
    : ICommandHandler<UnlockCellCommand>
{
    public async Task Handle(
        UnlockCellCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var cell =
            await context
                .Props.OfType<Cell>()
                .FirstOrDefaultAsync(cell => cell.Id == command.CellId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Cell), command.CellId);

        cell.IsLocked = false;
        cell.CreatureId = null;
        await context.SaveChangesAsync(cancellationToken);
    }
}
