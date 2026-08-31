using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class AddGoldCommand
{
    public required ItemOwnerReference Owner { get; init; }
    public required Guid WorldId { get; init; }
    public required int Amount { get; init; }
}

internal class AddGoldCommandHandler(TrpgDbContext context, GoldLoader goldLoader)
    : ICommandHandler<AddGoldCommand>
{
    public async Task Handle(AddGoldCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
        {
            throw new InvalidOperationException("Gold amount must be positive.");
        }

        var gold = await goldLoader.FindGold(command.Owner, cancellationToken);
        if (gold is null)
        {
            gold = new Gold
            {
                WorldId = command.WorldId,
                Name = "Gold",
                Ownership = new ItemOwnership
                {
                    OwnerId = command.Owner.Id,
                    OwnerType = command.Owner.Type,
                },
            };
            context.Items.Add(gold);
        }

        gold.Quantity += command.Amount;
        await context.SaveChangesAsync(cancellationToken);
    }
}
