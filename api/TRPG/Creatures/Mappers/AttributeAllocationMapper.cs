using TRPG.Creatures.Requests;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class AttributeAllocationMapper
{
    public static IReadOnlyDictionary<AllocatableAttributeName, int> ToDictionary(
        this AttributeAllocation allocation
    ) =>
        new Dictionary<AllocatableAttributeName, int>
        {
            [AllocatableAttributeName.Strength] = allocation.Strength,
            [AllocatableAttributeName.Dexterity] = allocation.Dexterity,
            [AllocatableAttributeName.Endurance] = allocation.Endurance,
            [AllocatableAttributeName.Stamina] = allocation.Stamina,
            [AllocatableAttributeName.Mana] = allocation.Mana,
            [AllocatableAttributeName.Intelligence] = allocation.Intelligence,
        };
}
