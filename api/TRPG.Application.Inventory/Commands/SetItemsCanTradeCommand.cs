using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory.Commands;

public class SetItemsCanTradeCommand
{
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
    public required bool CanTrade { get; init; }
}

internal class SetItemsCanTradeCommandHandler(IInventoryDbContext context)
    : ICommandHandler<SetItemsCanTradeCommand>
{
    public async Task Handle(
        SetItemsCanTradeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.ItemIds.Count == 0)
        {
            return;
        }

        await context
            .Items.Where(item => command.ItemIds.Contains(item.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.CanTrade, command.CanTrade),
                cancellationToken
            );
    }
}
