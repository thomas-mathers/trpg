using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes;

public static class PropOccupancyProjection
{
    public static IReadOnlyCollection<PropOccupancy> Diff(
        SceneResult scene,
        IReadOnlyDictionary<Guid, Guid?> occupantIdsBySeatId
    ) =>
        scene
            .NearbyProps.Where(prop => occupantIdsBySeatId.ContainsKey(prop.Id))
            .Select(prop => new
            {
                Prop = prop,
                Current = new PropOccupancy(
                    prop.Id,
                    occupantIdsBySeatId[prop.Id] is not null,
                    occupantIdsBySeatId[prop.Id] == scene.Player.Id
                ),
            })
            .Where(entry =>
                entry.Current.IsOccupied != entry.Prop.IsOccupied
                || entry.Current.IsOccupiedByPlayer != entry.Prop.IsOccupiedByPlayer
            )
            .Select(entry => entry.Current)
            .ToArray();

    public static SceneResult Apply(SceneResult scene, PropOccupancyChangedEvent change)
    {
        var occupancyByPropId = change.Props.ToDictionary(occupancy => occupancy.PropId);
        return scene with
        {
            NearbyProps = scene
                .NearbyProps.Select(prop =>
                    occupancyByPropId.TryGetValue(prop.Id, out var occupancy)
                        ? prop with
                        {
                            IsOccupied = occupancy.IsOccupied,
                            IsOccupiedByPlayer = occupancy.IsOccupiedByPlayer,
                        }
                        : prop
                )
                .ToArray(),
        };
    }
}
