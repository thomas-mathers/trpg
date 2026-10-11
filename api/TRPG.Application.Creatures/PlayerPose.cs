using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures;

public sealed record PlayerPose(
    Guid LocationId,
    Point Position,
    DateTimeOffset ReportedAt,
    MovementInput Input,
    TimeSpan Leftover
);
