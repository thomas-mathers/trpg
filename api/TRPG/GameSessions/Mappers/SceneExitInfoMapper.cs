using TRPG.Application.GameTurns.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneExitInfoMapper
{
    public static NearbyExitSnapshot ToSnapshot(this SceneExitInfo exit) =>
        new(exit.Description, exit.Destination.ToSnapshot());
}
