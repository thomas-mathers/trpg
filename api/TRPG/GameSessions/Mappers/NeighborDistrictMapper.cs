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
            Segments: neighbor.Segments.Select(segment => segment.ToSnapshot()).ToArray(),
            Roads: neighbor.Roads.Select(road => road.ToSnapshot()).ToArray()
        );
}
