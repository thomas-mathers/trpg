using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes;

internal static class SceneChangePlanner
{
    internal static IReadOnlyCollection<GameClientEvent> PlanCreatureChanges(
        SceneResult previous,
        SceneResult current,
        WorldStateStamp stamp
    )
    {
        var events = new List<GameClientEvent>();
        AddCreatureEvents(previous, current, stamp, events);
        return events;
    }

    private static void AddCreatureEvents(
        SceneResult previous,
        SceneResult current,
        WorldStateStamp stamp,
        List<GameClientEvent> events
    )
    {
        var oldCreatures = CreaturesById(previous);
        var newCreatures = CreaturesById(current);
        AddCreatureMembershipEvents(current, stamp, oldCreatures, newCreatures, events);
        AddCreatureChangeEvents(current, stamp, oldCreatures, newCreatures, events);
    }

    private static void AddCreatureMembershipEvents(
        SceneResult current,
        WorldStateStamp stamp,
        IReadOnlyDictionary<Guid, SceneCreatureInfo> oldCreatures,
        IReadOnlyDictionary<Guid, SceneCreatureInfo> newCreatures,
        List<GameClientEvent> events
    )
    {
        var left = oldCreatures.Keys.Except(newCreatures.Keys).ToHashSet();
        if (left.Count > 0)
        {
            events.Add(new CreaturesLeftEvent(current.WorldId, current.LocationId, stamp, left));
        }

        var arrived = newCreatures
            .Values.Where(creature => !oldCreatures.ContainsKey(creature.Id))
            .ToArray();
        if (arrived.Length > 0)
        {
            events.Add(
                new CreaturesArrivedEvent(current.WorldId, current.LocationId, stamp, arrived)
            );
        }
    }

    private static void AddCreatureChangeEvents(
        SceneResult current,
        WorldStateStamp stamp,
        IReadOnlyDictionary<Guid, SceneCreatureInfo> oldCreatures,
        IReadOnlyDictionary<Guid, SceneCreatureInfo> newCreatures,
        List<GameClientEvent> events
    )
    {
        var updated = new List<SceneCreatureInfo>();
        var moved = new Dictionary<Guid, Placement>();
        foreach (var creature in newCreatures.Values.Where(c => oldCreatures.ContainsKey(c.Id)))
        {
            var previous = oldCreatures[creature.Id];
            var sameStatus =
                creature.Id == current.Player.Id
                    ? SceneSemanticComparer.PlayerStatusEquivalent(previous, creature)
                    : SceneSemanticComparer.NearbyCreatureStatusEquivalent(previous, creature);
            if (!sameStatus)
            {
                updated.Add(creature);
            }
            else if (previous.Placement != creature.Placement)
            {
                moved.Add(creature.Id, creature.Placement);
            }
        }

        if (updated.Count > 0)
        {
            events.Add(
                new CreaturesUpdatedEvent(current.WorldId, current.LocationId, stamp, updated)
            );
        }
        if (moved.Count > 0)
        {
            events.Add(new CreaturesMovedEvent(current.WorldId, current.LocationId, stamp, moved));
        }
    }

    private static IReadOnlyDictionary<Guid, SceneCreatureInfo> CreaturesById(SceneResult scene) =>
        scene.NearbyCreatures.Append(scene.Player).ToDictionary(creature => creature.Id);
}
