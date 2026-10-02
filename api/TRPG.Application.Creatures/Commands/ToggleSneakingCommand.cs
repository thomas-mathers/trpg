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

public class ToggleSneakingCommand
{
    public required Guid CreatureId { get; init; }
}

public class ToggleSneakingCommandResult
{
    public required Guid CreatureId { get; init; }
    public required bool IsSneaking { get; init; }
    public required float MovementSpeed { get; init; }
}

internal class ToggleSneakingCommandHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
) : ICommandHandler<ToggleSneakingCommand, ToggleSneakingCommandResult>
{
    public async Task<ToggleSneakingCommandResult> Handle(
        ToggleSneakingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature =
            await context.Creatures.FirstOrDefaultAsync(
                c => c.Id == command.CreatureId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Creature with ID {command.CreatureId} not found."
            );

        if (creature.Condition != CreatureCondition.Awake)
        {
            throw new InvalidOperationException(
                "Cannot toggle sneaking for a creature that is not awake."
            );
        }

        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(command.CreatureId, OwnerType.Creature),
            },
            cancellationToken
        );
        var equippedItems = items.Where(item => item.Ownership.EquippedSlot != null).ToArray();

        creature.IsSneaking = !creature.IsSneaking;
        StatFormulas.RefreshMovementSpeed(creature, equippedItems, optionsSnapshot.Value);
        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);

        return new ToggleSneakingCommandResult
        {
            CreatureId = creature.Id,
            IsSneaking = creature.IsSneaking,
            MovementSpeed = creature.BaseAttributes.MovementSpeed,
        };
    }
}
