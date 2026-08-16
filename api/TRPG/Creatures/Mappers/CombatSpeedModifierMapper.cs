using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class CombatSpeedModifierMapper
{
    public static CombatSpeedModifierSummary ToSummary(this CombatSpeedModifier modifier) =>
        new(modifier.Amount, modifier.SpeedType.ToContract());
}
