using TRPG.Domain.Models;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class PointMapper
{
    public static PointWire ToWire(this Point point) => new(point.X, point.Y);
}
