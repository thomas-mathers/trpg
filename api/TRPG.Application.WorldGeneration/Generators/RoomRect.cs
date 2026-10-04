using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal readonly record struct RoomRect(double Left, double Top, double Width, double Depth)
{
    private const double Tolerance = 1e-9;

    internal double CenterX => Left + Width / 2;

    internal double CenterY => Top + Depth / 2;

    internal bool IsInside(Footprint room) =>
        Left >= -Tolerance
        && Top >= -Tolerance
        && Left + Width <= room.Width + Tolerance
        && Top + Depth <= room.Depth + Tolerance;

    internal bool IsWithin(RoomRect other, double margin) =>
        Left < other.Left + other.Width + margin
        && Left + Width + margin > other.Left
        && Top < other.Top + other.Depth + margin
        && Top + Depth + margin > other.Top;
}
