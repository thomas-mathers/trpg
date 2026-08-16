using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class LeechModifierMapper
{
    public static LeechModifierSummary ToSummary(this LeechModifier modifier) =>
        new(modifier.LeechType.ToContract(), modifier.Percent);
}
