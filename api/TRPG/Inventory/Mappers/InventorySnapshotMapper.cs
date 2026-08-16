using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Creatures.Mappers;

namespace TRPG.Inventory.Mappers;

internal static class InventorySnapshotMapper
{
    public static InventorySummary ToSummary(
        this InventorySnapshot snapshot,
        IReadOnlyCollection<Guid> questItemIds
    ) => new(snapshot.Gold, snapshot.Items.ToDetails(questItemIds));
}
