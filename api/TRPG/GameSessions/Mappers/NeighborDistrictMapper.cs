using TRPG.Application.Scenes.Neighbors;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class NeighborDistrictMapper
{
    public static NeighborSnapshot ToSnapshot(this NeighborDistrict neighbor) =>
        new(
            LocationId: neighbor.LocationId,
            Buildings: neighbor.Buildings.Select(building => building.ToSnapshot()).ToArray(),
            Props: neighbor.Props.Select(prop => prop.ToSnapshot()).ToArray(),
            GreenSpaces: neighbor
                .GreenSpaces.Select(space => new GreenSpaceSnapshot(
                    space.Id,
                    new PlacementWire(space.Placement.X, space.Placement.Y, space.Placement.Angle),
                    new FootprintWire(space.Footprint.Width, space.Footprint.Depth)
                ))
                .ToArray(),
            Segments: neighbor.Segments.Select(segment => segment.ToSnapshot()).ToArray(),
            Roads: neighbor.Roads.Select(road => road.ToSnapshot()).ToArray()
        );
}
