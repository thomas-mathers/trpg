using TRPG.Application.Creatures;

namespace TRPG.Application.WorldSimulation.Poses;

public sealed class LivePlayerPoses
{
    private readonly Dictionary<Guid, PlayerPose> _poses = [];
    private readonly HashSet<Guid> _dirtyPlayerIds = [];

    public bool HasUnsavedPoses => _dirtyPlayerIds.Count > 0;

    public PlayerPose? Find(Guid playerId) => _poses.GetValueOrDefault(playerId);

    public void Set(Guid playerId, PlayerPose pose)
    {
        _poses[playerId] = pose;
        _dirtyPlayerIds.Add(playerId);
    }

    public void Forget(Guid playerId)
    {
        _poses.Remove(playerId);
        _dirtyPlayerIds.Remove(playerId);
    }

    public IReadOnlyList<KeyValuePair<Guid, PlayerPose>> Unsaved() =>
        [
            .. _dirtyPlayerIds
                .Where(_poses.ContainsKey)
                .Select(playerId => KeyValuePair.Create(playerId, _poses[playerId])),
        ];

    public void MarkAllSaved() => _dirtyPlayerIds.Clear();
}
