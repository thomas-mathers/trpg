using Effect = TRPG.Application.Effects.ActiveHot;
using StoredEffect = TRPG.Domain.Models.ActiveHot;

namespace TRPG.Application.Effects.Mappers;

public static class ActiveHotMapper
{
    public static StoredEffect ToModel(this Effect effect) =>
        new()
        {
            AbilityName = effect.AbilityName,
            Amount = effect.Amount,
            NextTickAt = effect.NextTickAt,
            ExpiresAt = effect.ExpiresAt,
        };
}
