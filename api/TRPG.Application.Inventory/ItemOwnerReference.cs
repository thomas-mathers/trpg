using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

public record ItemOwnerReference(Guid Id, OwnerType Type);
