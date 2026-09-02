using TRPG.Application.Inventory.Results;
using TRPG.Creatures.Mappers;
using TRPG.Inventory.Responses;

namespace TRPG.Inventory.Mappers;

internal static class InventorySnapshotMapper
{
    public static InventorySummary ToSummary(
        this InventoryResult snapshot,
        IReadOnlyCollection<Guid> questItemIds,
        int? carryingCapacity = null
    ) =>
        new(
            snapshot.Gold,
            snapshot.Items.ToDetails(questItemIds),
            snapshot.Weight,
            carryingCapacity
        );
}
