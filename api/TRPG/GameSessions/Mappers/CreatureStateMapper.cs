using ContractCreatureState = TRPG.Contracts.Scenes.Responses.CreatureState;
using DataCreatureState = TRPG.Data.Models.CreatureState;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureStateMapper
{
    public static ContractCreatureState ToContract(this DataCreatureState state) =>
        state switch
        {
            DataCreatureState.Idle => ContractCreatureState.Idle,
            DataCreatureState.Sleeping => ContractCreatureState.Sleeping,
            DataCreatureState.Busy => ContractCreatureState.Busy,
            DataCreatureState.Studying => ContractCreatureState.Studying,
            DataCreatureState.Praying => ContractCreatureState.Praying,
            DataCreatureState.Training => ContractCreatureState.Training,
            DataCreatureState.Sitting => ContractCreatureState.Sitting,
            DataCreatureState.Alerted => ContractCreatureState.Alerted,
            DataCreatureState.Dead => ContractCreatureState.Dead,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
}
