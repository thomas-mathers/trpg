using TRPG.Domain.Models;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class PlacementMapper
{
    public static PlacementWire ToWire(this Placement placement) =>
        new(placement.X, placement.Y, placement.Angle);
}
