using ContractCreaturePosture = TRPG.GameSessions.Responses.CreaturePosture;
using DataCreaturePosture = TRPG.Domain.Models.CreaturePosture;

namespace TRPG.GameSessions.Mappers;

internal static class CreaturePostureMapper
{
    public static ContractCreaturePosture ToResponse(this DataCreaturePosture posture) =>
        posture switch
        {
            DataCreaturePosture.Standing => ContractCreaturePosture.Standing,
            DataCreaturePosture.Sitting => ContractCreaturePosture.Sitting,
            DataCreaturePosture.Lying => ContractCreaturePosture.Lying,
        };
}
