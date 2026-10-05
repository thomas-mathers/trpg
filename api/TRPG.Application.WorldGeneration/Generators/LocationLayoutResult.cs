using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record LocationLayoutResult(
    IReadOnlyList<Prop> Props,
    IReadOnlyList<RoadNode> RoadNodes,
    IReadOnlyList<RoadEdge> RoadEdges
);
