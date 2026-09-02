using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory.Commands;

public class SetItemsQuestLockedCommand
{
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
    public required bool IsQuestItem { get; init; }
}

internal class SetItemsQuestLockedCommandHandler(IInventoryDbContext context)
    : ICommandHandler<SetItemsQuestLockedCommand>
{
    public async Task Handle(
        SetItemsQuestLockedCommand command,
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
                setters => setters.SetProperty(item => item.IsQuestItem, command.IsQuestItem),
                cancellationToken
            );
    }
}
