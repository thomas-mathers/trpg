using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

internal class PreviewEquipItemStatsQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class PreviewEquipItemStatsQueryHandler(TrpgDbContext context)
{
    public async Task<Attributes> Handle(
        PreviewEquipItemStatsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        var items = await context
            .Items.AsNoTracking()
            .Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == query.CreatureId
            )
            .ToArrayAsync(cancellationToken);

        var toEquip = items.First(i => i.Id == query.ItemId);
        var currentlyEquipped = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        var conflicting = ItemEquipmentPolicy.GetConflictingItems(
            toEquip,
            query.Slot,
            currentlyEquipped
        );
        var conflictingIds = conflicting.Select(i => i.Id).ToHashSet();

        var hypotheticalEquipped = currentlyEquipped
            .Where(i => i.Id != toEquip.Id && !conflictingIds.Contains(i.Id))
            .Append(toEquip)
            .ToArray();

        var buffs = CreatureAttributesRecalculator.ToActiveBuffs(creature);

        return StatFormulas.CalculateEffectiveAttributes(
            creature.BaseAttributes,
            buffs,
            hypotheticalEquipped
        );
    }
}
