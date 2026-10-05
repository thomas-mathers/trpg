using TRPG.Application.Scenes.Roads;
using TRPG.Domain.Models;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class DistrictRoadMapper
{
    public static RoadSnapshot ToSnapshot(this DistrictRoad road) =>
        new(
            Points: road.Points.Select(point => point.ToWire()).ToArray(),
            Width: road.Width,
            Class: road.Class.ToSnapshot()
        );

    private static RoadClassSnapshot ToSnapshot(this RoadClass roadClass) =>
        roadClass switch
        {
            RoadClass.Avenue => RoadClassSnapshot.Avenue,
            RoadClass.Street => RoadClassSnapshot.Street,
            RoadClass.Lane => RoadClassSnapshot.Lane,
        };
}
