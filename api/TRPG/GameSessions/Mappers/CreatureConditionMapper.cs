using ContractCreatureCondition = TRPG.GameSessions.Responses.CreatureCondition;
using DataCreatureCondition = TRPG.Domain.Models.CreatureCondition;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureConditionMapper
{
    public static ContractCreatureCondition ToResponse(this DataCreatureCondition value) =>
        value switch
        {
            DataCreatureCondition.Awake => ContractCreatureCondition.Awake,
            DataCreatureCondition.Sleeping => ContractCreatureCondition.Sleeping,
            DataCreatureCondition.Dead => ContractCreatureCondition.Dead,
        };
}
