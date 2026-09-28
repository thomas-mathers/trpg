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
            DataCreatureState.Working => ContractCreatureState.Working,
            DataCreatureState.Studying => ContractCreatureState.Studying,
            DataCreatureState.Praying => ContractCreatureState.Praying,
            DataCreatureState.Eating => ContractCreatureState.Eating,
            DataCreatureState.Dead => ContractCreatureState.Dead,
            DataCreatureState.Walking => ContractCreatureState.Walking,
        };
}
