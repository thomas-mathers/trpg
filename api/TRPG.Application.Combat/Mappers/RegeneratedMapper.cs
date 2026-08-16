using TRPG.Application.Combat.Events;
using ContractEvent = TRPG.Application.Combat.Responses.CombatRegeneratedEvent;

namespace TRPG.Application.Combat.Mappers;

internal static class RegeneratedMapper
{
    public static ContractEvent ToContract(this Regenerated value) =>
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
