using ContractStairDirection = TRPG.GameSessions.Responses.StairDirection;
using DataStairDirection = TRPG.Domain.Models.StairDirection;

namespace TRPG.GameSessions.Mappers;

internal static class StairDirectionMapper
{
    public static ContractStairDirection ToResponse(this DataStairDirection direction) =>
        direction switch
        {
            DataStairDirection.Up => ContractStairDirection.Up,
            DataStairDirection.Down => ContractStairDirection.Down,
        };
}
