using Effect = TRPG.Application.Effects.ActiveDot;
using StoredEffect = TRPG.Domain.Models.ActiveDot;

namespace TRPG.Application.Effects.Mappers;

public static class ActiveDotMapper
{
    public static StoredEffect ToModel(this Effect effect) =>
        new()
        {
            AbilityName = effect.AbilityName,
            Amount = effect.Amount,
            DamageType = effect.DamageType.ToString(),
            NextTickAt = effect.NextTickAt,
            ExpiresAt = effect.ExpiresAt,
        };
}
