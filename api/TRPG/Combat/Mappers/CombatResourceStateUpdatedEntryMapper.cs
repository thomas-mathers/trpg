using TRPG.Application.Combat.Events;
using CombatResourceStateUpdatedEntry = TRPG.Combat.ClientModels.CombatResourceStateUpdatedEntry;

namespace TRPG.Combat.Mappers;

internal static class CombatResourceStateUpdatedEntryMapper
{
    public static CombatResourceStateUpdatedEntry ToContract(this ResourceStateUpdated value) =>
        new(
            value.CombatantId,
            value.CombatantName,
            value.CurrentAp,
            value.MaximumAp,
            value.CurrentMp,
            value.MaximumMp
        );
}
