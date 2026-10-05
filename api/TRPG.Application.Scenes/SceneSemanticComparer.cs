using TRPG.Application.Creatures.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes;

public static class SceneSemanticComparer
{
    public static bool HasPlayerVisibleChange(SceneResult previous, SceneResult current) =>
        HasStaticChange(previous, current)
        || previous.Weather != current.Weather
        || !PlayerStatusEquivalent(previous.Player, current.Player)
        || previous.Player.Placement != current.Player.Placement
        || !CreaturesAreEquivalent(previous.NearbyCreatures, current.NearbyCreatures)
        || !CaravansAreEquivalent(previous.NearbyCaravans, current.NearbyCaravans);

    internal static bool HasStaticChange(SceneResult previous, SceneResult current) =>
        previous.WorldId != current.WorldId
        || previous.LocationId != current.LocationId
        || previous.Player.Id != current.Player.Id
        || previous.State != current.State
        || previous.City != current.City
        || previous.District != current.District
        || previous.Building != current.Building
        || previous.Room != current.Room
        || !SetEquals(previous.Exits, current.Exits)
        || !SetEquals(previous.NearbyProps, current.NearbyProps)
        || !SetEquals(previous.NearbyBuildings, current.NearbyBuildings)
        || !SetEquals(previous.GreenSpaces ?? [], current.GreenSpaces ?? [])
        || previous.Size != current.Size;

    internal static bool PlayerStatusEquivalent(
        SceneCreatureInfo previous,
        SceneCreatureInfo current
    ) =>
        Normalize(previous) == Normalize(current)
        && previous.FactionNames.Order().SequenceEqual(current.FactionNames.Order())
        && SetEquals(previous.QuestMarkers, current.QuestMarkers)
        && EffectsAreEquivalent(previous.Effects, current.Effects);

    internal static bool NearbyCreatureStatusEquivalent(
        SceneCreatureInfo previous,
        SceneCreatureInfo current
    ) =>
        PlayerStatusEquivalent(previous, current)
        && previous.CurrentHp == current.CurrentHp
        && previous.CurrentAp == current.CurrentAp
        && previous.CurrentMp == current.CurrentMp;

    internal static bool CaravanStatusEquivalent(
        SceneCaravanInfo previous,
        SceneCaravanInfo current
    ) =>
        Normalize(previous) == Normalize(current)
        && SetEquals(previous.Destinations, current.Destinations);

    private static bool CreaturesAreEquivalent(
        IReadOnlyCollection<SceneCreatureInfo> previous,
        IReadOnlyCollection<SceneCreatureInfo> current
    )
    {
        var currentById = current.ToDictionary(creature => creature.Id);
        return previous.Count == current.Count
            && previous.All(creature =>
                currentById.TryGetValue(creature.Id, out var counterpart)
                && NearbyCreatureStatusEquivalent(creature, counterpart)
                && creature.Placement == counterpart.Placement
            );
    }

    private static bool CaravansAreEquivalent(
        IReadOnlyCollection<SceneCaravanInfo> previous,
        IReadOnlyCollection<SceneCaravanInfo> current
    )
    {
        var currentById = current.ToDictionary(caravan => caravan.CaravanId);
        return previous.Count == current.Count
            && previous.All(caravan =>
                currentById.TryGetValue(caravan.CaravanId, out var counterpart)
                && CaravanStatusEquivalent(caravan, counterpart)
            );
    }

    private static SceneCreatureInfo Normalize(SceneCreatureInfo creature) =>
        creature with
        {
            CurrentHp = 0,
            CurrentAp = 0,
            CurrentMp = 0,
            FactionNames = Array.Empty<string>(),
            QuestMarkers = Array.Empty<QuestMarkerEntry>(),
            Effects = CreatureEffects.None,
            Placement = new(0, 0, 0),
        };

    private static SceneCaravanInfo Normalize(SceneCaravanInfo caravan) =>
        caravan with
        {
            MinutesUntilDeparture = 0,
            Destinations = Array.Empty<SceneCaravanDestination>(),
        };

    private static bool EffectsAreEquivalent(CreatureEffects previous, CreatureEffects current) =>
        SetEquals(previous.Conditions, current.Conditions)
        && SetEquals(previous.Dots, current.Dots)
        && SetEquals(previous.Hots, current.Hots)
        && SetEquals(previous.Buffs, current.Buffs);

    private static bool SetEquals<T>(
        IReadOnlyCollection<T> previous,
        IReadOnlyCollection<T> current
    ) => previous.Count == current.Count && previous.ToHashSet().SetEquals(current);
}
