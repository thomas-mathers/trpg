using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class ProcModifierMapper
{
    public static ProcModifierSummary ToSummary(this ProcModifier modifier) =>
        new(modifier.AbilityName, modifier.Chance, modifier.Trigger.ToContract());
}
