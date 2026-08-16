using TRPG.Application.Combat.Events;
using CombatRegeneratedEntry = TRPG.Combat.ClientModels.CombatRegeneratedEntry;

namespace TRPG.Combat.Mappers;

internal static class CombatRegeneratedEntryMapper
{
    public static CombatRegeneratedEntry ToContract(this Regenerated value) =>
        new(
            value.CombatantId,
            value.CombatantName,
            value.PreviousAp,
            value.CurrentAp,
            value.MaximumAp,
            value.PreviousMp,
            value.CurrentMp,
            value.MaximumMp
        );
}
