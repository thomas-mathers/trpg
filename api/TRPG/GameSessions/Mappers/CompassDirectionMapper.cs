using ContractCompassDirection = TRPG.GameSessions.Responses.CompassDirection;
using DataCompassDirection = TRPG.Domain.Models.CompassDirection;

namespace TRPG.GameSessions.Mappers;

internal static class CompassDirectionMapper
{
    public static ContractCompassDirection ToResponse(this DataCompassDirection direction) =>
        direction switch
        {
            DataCompassDirection.North => ContractCompassDirection.North,
            DataCompassDirection.Northeast => ContractCompassDirection.Northeast,
            DataCompassDirection.East => ContractCompassDirection.East,
            DataCompassDirection.Southeast => ContractCompassDirection.Southeast,
            DataCompassDirection.South => ContractCompassDirection.South,
            DataCompassDirection.Southwest => ContractCompassDirection.Southwest,
            DataCompassDirection.West => ContractCompassDirection.West,
            DataCompassDirection.Northwest => ContractCompassDirection.Northwest,
        };
}
