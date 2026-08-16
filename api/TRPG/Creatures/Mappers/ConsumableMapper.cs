using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ConsumableMapper
{
    public static ConsumableSummary ToSummary(this Consumable item) =>
        new(item.Id, item.Name, item.Quantity, item.Resource.ToResponse(), item.RestoreAmount);
}
