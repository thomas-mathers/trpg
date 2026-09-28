using ContractCreatureActivity = TRPG.GameSessions.Responses.CreatureActivity;
using DataCreatureActivity = TRPG.Domain.Models.CreatureActivity;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureActivityMapper
{
    public static ContractCreatureActivity ToResponse(this DataCreatureActivity value) =>
        value switch
        {
            DataCreatureActivity.Working => ContractCreatureActivity.Working,
            DataCreatureActivity.Studying => ContractCreatureActivity.Studying,
            DataCreatureActivity.Praying => ContractCreatureActivity.Praying,
            DataCreatureActivity.Eating => ContractCreatureActivity.Eating,
        };
}
