using TRPG.Application.Scenes.Events;
using ApplicationCreatureMovementDirection = TRPG.Application.Scenes.Events.CreatureMovementDirection;
using ContractCreatureMovementDirection = TRPG.GameSessions.Responses.CreatureMovementDirection;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureMovementDirectionMapper
{
    public static ContractCreatureMovementDirection ToResponse(
        this ApplicationCreatureMovementDirection direction
    ) =>
        direction switch
        {
            ApplicationCreatureMovementDirection.Arrived =>
                ContractCreatureMovementDirection.Arrived,
            ApplicationCreatureMovementDirection.Departed =>
                ContractCreatureMovementDirection.Departed,
        };
}
