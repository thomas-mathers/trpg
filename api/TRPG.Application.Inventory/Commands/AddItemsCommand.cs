using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class AddItemsCommand
{
    public required IReadOnlyCollection<Item> Items { get; init; }
}

internal class AddItemsCommandHandler(TrpgDbContext context) : ICommandHandler<AddItemsCommand>
{
    public async Task Handle(AddItemsCommand command, CancellationToken cancellationToken = default)
    {
        context.Items.AddRange(command.Items);
        await context.SaveChangesAsync(cancellationToken);
    }
}
