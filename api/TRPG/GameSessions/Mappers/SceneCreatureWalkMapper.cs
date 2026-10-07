using TRPG.Application.Scenes.Results;
using TRPG.Combat.Mappers;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneCreatureWalkMapper
{
    public static CreatureWalkSnapshot ToWire(this SceneCreatureWalk walk) =>
        new(
            walk.Path.Select(point => point.ToWire()).ToArray(),
            walk.StartedAt.ToGameTimeMilliseconds(),
            walk.MetersPerGameSecond,
            walk.LeavesAtEnd,
            walk.PausedAt?.ToGameTimeMilliseconds()
        );
}
