using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal record ItemOwnerReference(Guid Id, OwnerType Type);
