using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ProcModifierMapper
{
    public static ProcModifierSummary ToSummary(this ProcModifier modifier) =>
        new(modifier.AbilityName, modifier.Chance, modifier.Trigger.ToResponse());
}
