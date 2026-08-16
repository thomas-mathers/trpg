using ContractResourceType = TRPG.Inventory.Responses.ResourceType;
using DataResourceType = TRPG.Domain.Models.ResourceType;

namespace TRPG.Creatures.Mappers;

internal static class ResourceTypeMapper
{
    public static ContractResourceType ToResponse(this DataResourceType resource) =>
        resource switch
        {
            DataResourceType.Hp => ContractResourceType.Hp,
            DataResourceType.Ap => ContractResourceType.Ap,
            DataResourceType.Mp => ContractResourceType.Mp,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
        };
}
