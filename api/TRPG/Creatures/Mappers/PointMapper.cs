using TRPG.Creatures.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class PointMapper
{
    public static PointResponse ToMapResponse(this Point point) => new(X: point.X, Y: point.Y);
}
