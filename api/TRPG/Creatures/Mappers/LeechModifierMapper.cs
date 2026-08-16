using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class LeechModifierMapper
{
    public static LeechModifierSummary ToSummary(this LeechModifier modifier) =>
        new(modifier.LeechType.ToResponse(), modifier.Percent);
}
