using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class ConsumableMapper
{
    public static ConsumableSummary ToSummary(this Consumable item) =>
        new(item.Id, item.Name, item.Quantity, item.Resource.ToContract(), item.RestoreAmount);
}
