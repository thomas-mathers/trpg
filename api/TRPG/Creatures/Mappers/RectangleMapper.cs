using TRPG.Creatures.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class RectangleMapper
{
    public static RoomBoundsResponse ToMapResponse(this Rectangle bounds) =>
        new(Left: bounds.Left, Top: bounds.Top, Right: bounds.Right, Bottom: bounds.Bottom);
}
