using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class LeechModifierMapper
{
    public static LeechModifierSummary ToSummary(this LeechModifier modifier) =>
        new(modifier.LeechType.ToContract(), modifier.Percent);
}
