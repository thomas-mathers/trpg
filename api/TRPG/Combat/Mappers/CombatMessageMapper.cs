using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;

namespace TRPG.Combat.Mappers;

internal static class CombatMessageMapper
{
    public static IReadOnlyList<string> ToCombatMessages(
        this IReadOnlyList<CombatResolution> resolutions
    ) =>
        resolutions
            .Where(resolution =>
                resolution
                    is Healed
                        or ConsumedPotion
                        or BuffApplied
                        or HealOverTimeApplied
                        or FleeFailed
            )
            .Select(CombatNarration.Describe)
            .OfType<string>()
            .ToArray();
}
