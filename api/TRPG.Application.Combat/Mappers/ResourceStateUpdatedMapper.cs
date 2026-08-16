using TRPG.Application.Combat.Events;
using ContractEvent = TRPG.Application.Combat.Responses.CombatResourceStateUpdatedEvent;

namespace TRPG.Application.Combat.Mappers;

internal static class ResourceStateUpdatedMapper
{
    public static ContractEvent ToContract(this ResourceStateUpdated value) =>
        new(
            value.CombatantId,
            value.CombatantName,
            value.CurrentAp,
            value.MaximumAp,
            value.CurrentMp,
            value.MaximumMp
        );
}
