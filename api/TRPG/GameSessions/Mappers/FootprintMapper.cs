using TRPG.Domain.Models;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class FootprintMapper
{
    public static FootprintWire ToWire(this Footprint footprint) =>
        new(footprint.Width, footprint.Depth);
}
