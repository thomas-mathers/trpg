using ContractCreatureState = TRPG.GameSessions.Responses.CreatureState;
using DataCreatureState = TRPG.Domain.Models.CreatureState;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureStateMapper
{
    public static ContractCreatureState ToResponse(this DataCreatureState state) =>
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
