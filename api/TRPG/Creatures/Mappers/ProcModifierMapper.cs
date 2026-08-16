using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class ProcModifierMapper
{
    public static ProcModifierSummary ToSummary(this ProcModifier modifier) =>
        new(modifier.AbilityName, modifier.Chance, modifier.Trigger.ToContract());
}
