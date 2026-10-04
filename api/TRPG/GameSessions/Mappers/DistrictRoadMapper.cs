using TRPG.Application.Scenes.Roads;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class DistrictRoadMapper
{
    public static RoadSnapshot ToSnapshot(this DistrictRoad road) =>
        new(Points: road.Points.Select(point => point.ToWire()).ToArray(), Width: road.Width);
}
