using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

public record ItemOwnerReference(Guid Id, OwnerType Type);
