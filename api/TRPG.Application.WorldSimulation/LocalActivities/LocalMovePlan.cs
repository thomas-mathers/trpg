using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.LocalActivities;

public enum LocalMoveTargetKind
{
    Workstation,
    Seat,
    Bed,
}

public sealed record LocalMovePlan(
    Guid CreatureId,
    Guid LocationId,
    Guid JobId,
    CreatureJobAction Action,
    LocalMoveTargetKind TargetKind,
    Guid TargetPropId,
    IReadOnlyList<Point> Path
);

public sealed record LocalActivityPlanResult(bool Handled, LocalMovePlan? Move = null);
