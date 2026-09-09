using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class Bearings
{
    private const double DegreesPerPoint = 45;

    // Which compass point one place lies on from another. North is decreasing Y, matching how the
    // world map already draws these coordinates, so north is up on any map made from them. Nothing
    // else in the game defines north, so this is the definition; measure every bearing through here.
    public static CompassDirection Between(Point origin, Point destination)
    {
        var eastward = destination.X - origin.X;
        var northward = origin.Y - destination.Y;

        var degrees = Math.Atan2(eastward, northward) * 180 / Math.PI;
        if (degrees < 0)
        {
            degrees += 360;
        }

        return (CompassDirection)((int)Math.Round(degrees / DegreesPerPoint) % 8);
    }

    public static string ToWords(CompassDirection direction) =>
        direction.ToString().ToLowerInvariant();
}
