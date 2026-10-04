using TRPG.Application.Scenes.Boundaries;
using TRPG.GameSessions.Responses;
using ContractBoundarySegmentKind = TRPG.GameSessions.Responses.BoundarySegmentKind;
using SourceBoundarySegmentKind = TRPG.Application.Scenes.Boundaries.BoundarySegmentKind;

namespace TRPG.GameSessions.Mappers;

internal static class DistrictBoundaryMapper
{
    public static LocationBoundarySnapshot ToSnapshot(this DistrictBoundary boundary) =>
        new(
            Segments: boundary.Segments.Select(segment => segment.ToSnapshot()).ToArray(),
            Gates: boundary.Gates.Select(gate => gate.ToSnapshot()).ToArray(),
            OpenEdges: boundary.OpenEdges.Select(edge => edge.ToResponse()).ToArray()
        );

    private static BoundarySegmentSnapshot ToSnapshot(this BoundarySegment segment) =>
        new(
            Kind: segment.Kind.ToResponse(),
            Placement: segment.Placement.ToWire(),
            Footprint: segment.Footprint.ToWire()
        );

    private static BoundaryGateSnapshot ToSnapshot(this BoundaryGate gate) =>
        new(ConnectorId: gate.ConnectorId, Placement: gate.Placement.ToWire(), Width: gate.Width);

    private static ContractBoundarySegmentKind ToResponse(this SourceBoundarySegmentKind kind) =>
        kind switch
        {
            SourceBoundarySegmentKind.Wall => ContractBoundarySegmentKind.Wall,
            SourceBoundarySegmentKind.Tower => ContractBoundarySegmentKind.Tower,
        };
}
