using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class CombatSpeedModifierMapper
{
    public static CombatSpeedModifierSummary ToSummary(this CombatSpeedModifier modifier) =>
        new(modifier.Amount, modifier.SpeedType.ToResponse());
}
