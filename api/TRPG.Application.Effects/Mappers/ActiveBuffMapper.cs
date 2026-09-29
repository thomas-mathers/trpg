using Effect = TRPG.Application.CreatureFormulas.ActiveBuff;
using StoredEffect = TRPG.Domain.Models.ActiveBuff;

namespace TRPG.Application.Effects.Mappers;

public static class ActiveBuffMapper
{
    public static StoredEffect ToModel(this Effect effect) =>
        new()
        {
            AbilityName = effect.AbilityName,
            Amount = effect.Amount,
            Attribute = effect.Attribute.ToString(),
            ExpiresAt = effect.ExpiresAt,
            AmountType = effect.AmountType.ToString(),
        };
}
