using System.Collections.Concurrent;

namespace TRPG.Application.Creatures;

internal sealed record PlayerPose(
    Guid LocationId,
    double X,
    double Y,
    double Angle,
    DateTimeOffset ReportedAt,
    bool IsDirty
);

internal sealed class PlayerPoseStore
{
    private readonly ConcurrentDictionary<Guid, PlayerPose> _poses = new();

    public PlayerPose? Find(Guid playerId) => _poses.GetValueOrDefault(playerId);

    public void Set(Guid playerId, PlayerPose pose) => _poses[playerId] = pose;

    public void Remove(Guid playerId) => _poses.TryRemove(playerId, out _);

    public PlayerPose? TakeDirty(Guid playerId)
    {
        while (_poses.TryGetValue(playerId, out var pose) && pose.IsDirty)
        {
            if (_poses.TryUpdate(playerId, pose with { IsDirty = false }, pose))
            {
                return pose;
            }
        }

        return null;
    }
}
