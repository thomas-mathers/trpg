using TRPG.Application.GameTurns.Results;
using TRPG.Application.Quests.Queries;

namespace TRPG.Application.GameTurns;

public static class SceneSemanticComparer
{
    // The clock, vital meters, and departure countdowns tick continuously and have their own
    // delivery paths, so they never make a scene worth re-sending on their own.
    public static bool HasPlayerVisibleChange(SceneResult previous, SceneResult current) =>
        previous.WorldId != current.WorldId
        || previous.State != current.State
        || previous.City != current.City
        || previous.District != current.District
        || previous.Building != current.Building
        || previous.Room != current.Room
        || previous.Weather != current.Weather
        || !AreEquivalent(previous.Player, current.Player)
        || !SetEquals(previous.Exits, current.Exits)
        || !SetEquals(previous.NearbyProps, current.NearbyProps)
        || !SetEquals(previous.NearbyBuildings, current.NearbyBuildings)
        || !CreaturesAreEquivalent(previous.NearbyCreatures, current.NearbyCreatures)
        || !CaravansAreEquivalent(previous.NearbyCaravans, current.NearbyCaravans);

    private static bool CreaturesAreEquivalent(
        IReadOnlyCollection<SceneCreatureInfo> previous,
        IReadOnlyCollection<SceneCreatureInfo> current
    )
    {
        var currentById = current.ToDictionary(creature => creature.Id);
        return previous.Count == current.Count
            && previous.All(creature =>
                currentById.TryGetValue(creature.Id, out var counterpart)
                && AreEquivalent(creature, counterpart)
            );
    }

    private static bool AreEquivalent(SceneCreatureInfo previous, SceneCreatureInfo current) =>
        Normalize(previous) == Normalize(current)
        && previous.FactionNames.Order().SequenceEqual(current.FactionNames.Order())
        && SetEquals(previous.QuestMarkers, current.QuestMarkers);

    private static SceneCreatureInfo Normalize(SceneCreatureInfo creature) =>
        creature with
        {
            CurrentHp = 0,
            CurrentAp = 0,
            CurrentMp = 0,
            FactionNames = Array.Empty<string>(),
            QuestMarkers = Array.Empty<QuestMarkerEntry>(),
        };

    private static bool CaravansAreEquivalent(
        IReadOnlyCollection<SceneCaravanInfo> previous,
        IReadOnlyCollection<SceneCaravanInfo> current
    )
    {
        var currentById = current.ToDictionary(caravan => caravan.CaravanId);
        return previous.Count == current.Count
            && previous.All(caravan =>
                currentById.TryGetValue(caravan.CaravanId, out var counterpart)
                && Normalize(caravan) == Normalize(counterpart)
                && SetEquals(caravan.Destinations, counterpart.Destinations)
            );
    }

    private static SceneCaravanInfo Normalize(SceneCaravanInfo caravan) =>
        caravan with
        {
            MinutesUntilDeparture = 0,
            Destinations = Array.Empty<SceneCaravanDestination>(),
        };

    private static bool SetEquals<T>(
        IReadOnlyCollection<T> previous,
        IReadOnlyCollection<T> current
    ) => previous.Count == current.Count && previous.ToHashSet().SetEquals(current);
}
