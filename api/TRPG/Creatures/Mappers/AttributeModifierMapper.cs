using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class AttributeModifierMapper
{
    public static AttributeModifierSummary ToSummary(this AttributeModifier modifier) =>
        new(modifier.Amount, modifier.Attribute, modifier.AmountType);
}
