using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class AttributeModifierMapper
{
    public static AttributeModifierSummary ToSummary(this AttributeModifier modifier) =>
        new(modifier.Amount, modifier.Attribute.ToContract(), modifier.AmountType.ToContract());
}
