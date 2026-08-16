using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class SpecialHitModifierMapper
{
    public static SpecialHitModifierSummary ToSummary(this SpecialHitModifier modifier) =>
        new(modifier.Chance, modifier.HitType.ToResponse());
}
