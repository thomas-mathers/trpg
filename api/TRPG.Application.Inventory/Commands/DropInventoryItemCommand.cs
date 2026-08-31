using TRPG.Application.Common.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class DropInventoryItemCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid ItemId { get; init; }
    public required int Quantity { get; init; }
}

internal class DropInventoryItemCommandHandler(
    ICommandHandler<RemoveInventoryItemsCommand> removeInventoryItems,
    QuestItemGuard questItemGuard
) : ICommandHandler<DropInventoryItemCommand>
{
    public async Task Handle(
        DropInventoryItemCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Quantity <= 0)
        {
            throw new InvalidOperationException("Drop quantity must be positive.");
        }

        await questItemGuard.EnsureNotActiveQuestItems(
            command.PlayerId,
            [command.ItemId],
            cancellationToken
        );

        await removeInventoryItems.Handle(
            new RemoveInventoryItemsCommand
            {
                Removals =
                [
                    new InventoryItemRemoval(command.PlayerId, command.ItemId, command.Quantity),
                ],
            },
            cancellationToken
        );
    }
}
