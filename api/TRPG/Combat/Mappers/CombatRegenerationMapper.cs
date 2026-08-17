using TRPG.Application.Combat.Events;
using TRPG.Combat.ClientModels;

namespace TRPG.Combat.Mappers;

internal static class CombatRegenerationMapper
{
    public static IReadOnlyList<CombatRegeneration> ToCombatRegenerations(
        this IReadOnlyList<CombatResolution> resolutions
    ) =>
        resolutions
            .OfType<Regenerated>()
            .Select(regenerated => new CombatRegeneration(
                regenerated.CombatantId,
                regenerated.PreviousAp,
                regenerated.CurrentAp,
                regenerated.MaximumAp,
                regenerated.PreviousMp,
                regenerated.CurrentMp,
                regenerated.MaximumMp
            ))
            .ToArray();
}
