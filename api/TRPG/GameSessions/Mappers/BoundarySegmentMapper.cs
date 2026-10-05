using TRPG.Application.Scenes.Boundaries;
using TRPG.GameSessions.Responses;
using ContractBoundarySegmentKind = TRPG.GameSessions.Responses.BoundarySegmentKind;
using SourceBoundarySegmentKind = TRPG.Application.Scenes.Boundaries.BoundarySegmentKind;

namespace TRPG.GameSessions.Mappers;

internal static class BoundarySegmentMapper
{
    public static BoundarySegmentSnapshot ToSnapshot(this BoundarySegment segment) =>
        new(
            Kind: segment.Kind.ToResponse(),
            Placement: segment.Placement.ToWire(),
            Footprint: segment.Footprint.ToWire()
        );

    private static ContractBoundarySegmentKind ToResponse(this SourceBoundarySegmentKind kind) =>
        kind switch
        {
            SourceBoundarySegmentKind.Wall => ContractBoundarySegmentKind.Wall,
            SourceBoundarySegmentKind.Tower => ContractBoundarySegmentKind.Tower,
        };
}
