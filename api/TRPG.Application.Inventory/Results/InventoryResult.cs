using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Results;

public record InventoryResult(int Gold, IReadOnlyList<Item> Items);
