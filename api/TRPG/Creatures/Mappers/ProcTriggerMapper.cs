using ContractProcTrigger = TRPG.Contracts.Inventory.Responses.ProcTrigger;
using DataProcTrigger = TRPG.Domain.Models.ProcTrigger;

namespace TRPG.Creatures.Mappers;

internal static class ProcTriggerMapper
{
    public static ContractProcTrigger ToContract(this DataProcTrigger trigger) =>
        trigger switch
        {
            DataProcTrigger.OnStriking => ContractProcTrigger.OnStriking,
            DataProcTrigger.WhenStruck => ContractProcTrigger.WhenStruck,
            DataProcTrigger.OnKill => ContractProcTrigger.OnKill,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };
}
