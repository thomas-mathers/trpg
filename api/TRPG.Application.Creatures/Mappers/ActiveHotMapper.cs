using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class ActiveHotMapper
{
    public static CreatureHotEffect ToEffect(this ActiveHot hot) =>
        new(hot.AbilityName, hot.Amount, hot.ExpiresAt);
}
