using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class AddItemsCommand
{
    public required IReadOnlyCollection<Item> Items { get; init; }
}

internal class AddItemsCommandHandler(IInventoryDbContext context)
    : ICommandHandler<AddItemsCommand>
{
    public async Task Handle(AddItemsCommand command, CancellationToken cancellationToken = default)
    {
        context.Items.AddRange(command.Items);
        await context.SaveChangesAsync(cancellationToken);
    }
}
