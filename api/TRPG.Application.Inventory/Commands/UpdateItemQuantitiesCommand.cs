using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory.Commands;

public record ItemQuantityUpdate(Guid ItemId, int NewQuantity);

public class UpdateItemQuantitiesCommand
{
    public required IReadOnlyCollection<ItemQuantityUpdate> Updates { get; init; }
}

internal class UpdateItemQuantitiesCommandHandler(IInventoryDbContext context)
    : ICommandHandler<UpdateItemQuantitiesCommand>
{
    public async Task Handle(
        UpdateItemQuantitiesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Updates.Count == 0)
        {
            return;
        }

        var ids = command.Updates.Select(u => u.ItemId).ToArray();
        var itemsById = await context
            .Items.Where(i => ids.AsEnumerable().Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        foreach (var update in command.Updates)
        {
            itemsById[update.ItemId].Quantity = update.NewQuantity;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
