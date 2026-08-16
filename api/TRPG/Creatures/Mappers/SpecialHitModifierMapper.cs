using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class SpecialHitModifierMapper
{
    public static SpecialHitModifierSummary ToSummary(this SpecialHitModifier modifier) =>
        new(modifier.Chance, modifier.HitType.ToContract());
}
