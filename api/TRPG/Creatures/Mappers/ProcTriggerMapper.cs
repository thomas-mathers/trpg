using ContractProcTrigger = TRPG.Inventory.Responses.ProcTrigger;
using DataProcTrigger = TRPG.Domain.Models.ProcTrigger;

namespace TRPG.Creatures.Mappers;

internal static class ProcTriggerMapper
{
    public static ContractProcTrigger ToResponse(this DataProcTrigger trigger) =>
        trigger switch
        {
            DataProcTrigger.OnStriking => ContractProcTrigger.OnStriking,
            DataProcTrigger.WhenStruck => ContractProcTrigger.WhenStruck,
            DataProcTrigger.OnKill => ContractProcTrigger.OnKill,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };
}
