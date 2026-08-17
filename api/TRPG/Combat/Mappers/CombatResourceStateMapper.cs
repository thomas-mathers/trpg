using TRPG.Application.Combat.Events;
using TRPG.Combat.ClientModels;

namespace TRPG.Combat.Mappers;

internal static class CombatResourceStateMapper
{
    public static IReadOnlyList<CombatResourceState> ToCombatResourceStates(
        this IReadOnlyList<CombatResolution> resolutions
    ) =>
        resolutions
            .OfType<ResourceStateUpdated>()
            .Select(resourceState => new CombatResourceState(
                resourceState.CombatantId,
                resourceState.CurrentAp,
                resourceState.MaximumAp,
                resourceState.CurrentMp,
                resourceState.MaximumMp
            ))
            .ToArray();
}
