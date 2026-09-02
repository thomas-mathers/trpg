using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;

namespace TRPG.Application.Inventory.Queries;

public class GetEquipItemStatsQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
    public required Attributes BaseAttributes { get; init; }
    public required IReadOnlyCollection<ActiveBuff> ActiveBuffs { get; init; }
}

internal class GetEquipItemStatsQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetEquipItemStatsQuery, Attributes>
{
    public async Task<Attributes> Handle(
        GetEquipItemStatsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var items = await context
            .Items.AsNoTracking()
            .Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == query.CreatureId
            )
            .ToArrayAsync(cancellationToken);

        var toEquip = items.First(i => i.Id == query.ItemId);
        var currentlyEquipped = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        var conflicting = EquipmentLoadoutPolicy.GetConflictingItems(
            toEquip,
            query.Slot,
            currentlyEquipped
        );
        var conflictingIds = conflicting.Select(i => i.Id).ToHashSet();

        var hypotheticalEquipped = currentlyEquipped
            .Where(i => i.Id != toEquip.Id && !conflictingIds.Contains(i.Id))
            .Append(toEquip)
            .ToArray();

        return StatFormulas.CalculateEffectiveAttributes(
            query.BaseAttributes,
            query.ActiveBuffs,
            hypotheticalEquipped
        );
    }
}
