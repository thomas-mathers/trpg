using TRPG.Application.Scenes.Boundaries;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class DistrictBoundaryMapper
{
    public static LocationBoundarySnapshot ToSnapshot(this DistrictBoundary boundary) =>
        new(
            Segments: boundary.Segments.Select(segment => segment.ToSnapshot()).ToArray(),
            Gates: boundary.Gates.Select(gate => gate.ToSnapshot()).ToArray(),
            OpenEdges: boundary.OpenEdges.Select(edge => edge.ToResponse()).ToArray()
        );

    private static BoundaryGateSnapshot ToSnapshot(this BoundaryGate gate) =>
        new(ConnectorId: gate.ConnectorId, Placement: gate.Placement.ToWire(), Width: gate.Width);
}
