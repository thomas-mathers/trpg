using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class SetSneakingCommand
{
    public required Guid CreatureId { get; init; }
    public required bool IsSneaking { get; init; }
}

internal class SetSneakingCommandHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
) : ICommandHandler<SetSneakingCommand>
{
    public async Task Handle(
        SetSneakingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context.Creatures.FirstOrDefaultAsync(
            c =>
                c.Id == command.CreatureId
                && (!command.IsSneaking || c.Condition == CreatureCondition.Awake),
            cancellationToken
        );
        if (creature == null)
        {
            return;
        }

        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(command.CreatureId, OwnerType.Creature),
            },
            cancellationToken
        );
        var equippedItems = items.Where(item => item.Ownership.EquippedSlot != null).ToArray();

        creature.IsSneaking = command.IsSneaking;
        StatFormulas.RefreshMovementSpeed(creature, equippedItems, optionsSnapshot.Value);
        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
