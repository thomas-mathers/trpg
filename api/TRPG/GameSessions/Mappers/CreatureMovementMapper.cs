using ContractCreatureMovement = TRPG.GameSessions.Responses.CreatureMovement;
using DataCreatureMovement = TRPG.Domain.Models.CreatureMovement;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureMovementMapper
{
    public static ContractCreatureMovement ToResponse(this DataCreatureMovement value) =>
        value switch
        {
            DataCreatureMovement.Stationary => ContractCreatureMovement.Stationary,
            DataCreatureMovement.Walking => ContractCreatureMovement.Walking,
        };
}
