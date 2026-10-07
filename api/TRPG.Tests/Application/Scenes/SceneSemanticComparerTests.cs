using TRPG.Application.Abilities;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes;

public class SceneSemanticComparerTests
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid VillagerId = Guid.NewGuid();
    private static readonly Guid CaravanId = Guid.NewGuid();
    private static readonly Guid ConnectorId = Guid.NewGuid();
    private static readonly Guid DestinationId = Guid.NewGuid();

    [Fact]
    public void ScenePublisher_EnqueuesAnUnchangedSceneOnlyOnce()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        var scene = MakeScene();

        publisher.PublishIfChanged(WorldId, PlayerId, scene, MakeStamp(1));
        publisher.PublishIfChanged(WorldId, PlayerId, scene, MakeStamp(2));

        Assert.Single(events.Events);
    }

    [Fact]
    public void ScenePublisher_EmitsGranularEventsForSameLocationChanges()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        publisher.Publish(
            WorldId,
            PlayerId,
            MakeScene(weather: WeatherCondition.Clear),
            MakeStamp(1)
        );
        events.Events.Clear();

        publisher.PublishIfChanged(
            WorldId,
            PlayerId,
            MakeScene(
                creatures: [MakeCreature(VillagerId)],
                caravans: [MakeCaravan(minutesUntilDeparture: 30)],
                weather: WeatherCondition.Rain
            ),
            MakeStamp(2)
        );

        var arrival = Assert.IsType<CreaturesArrivedEvent>(events.Events[0]);
        Assert.Equal(VillagerId, Assert.Single(arrival.Creatures).Id);
        Assert.Equal(2, arrival.Stamp.Version);
        Assert.IsType<CaravansArrivedEvent>(events.Events[1]);
        Assert.IsType<WeatherChangedEvent>(events.Events[2]);
        Assert.Equal(3, events.Events.Count);
    }

    [Fact]
    public void ScenePublisher_UpdatesStatusWithPlacementWithoutAlsoSendingMovement()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        publisher.Publish(
            WorldId,
            PlayerId,
            MakeScene(creatures: [MakeCreature(VillagerId)]),
            MakeStamp(1)
        );
        events.Events.Clear();

        publisher.PublishIfChanged(
            WorldId,
            PlayerId,
            MakeScene(
                creatures:
                [
                    MakeCreature(
                        VillagerId,
                        activity: CreatureActivity.Working,
                        placement: new Placement(6, 4, 0)
                    ),
                ]
            ),
            MakeStamp(2)
        );

        var update = Assert.IsType<CreaturesUpdatedEvent>(Assert.Single(events.Events));
        Assert.Equal(6, Assert.Single(update.Creatures).Placement.X);
    }

    [Fact]
    public void ScenePublisher_SendsMovementWithoutResendingUnchangedStatus()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        publisher.Publish(
            WorldId,
            PlayerId,
            MakeScene(creatures: [MakeCreature(VillagerId)]),
            MakeStamp(1)
        );
        events.Events.Clear();

        publisher.PublishIfChanged(
            WorldId,
            PlayerId,
            MakeScene(creatures: [MakeCreature(VillagerId, placement: new Placement(6, 4, 0))]),
            MakeStamp(2)
        );

        var moved = Assert.IsType<CreaturesMovedEvent>(Assert.Single(events.Events));
        Assert.Equal(new Placement(6, 4, 0), moved.CreaturePlacements[VillagerId]);
    }

    [Fact]
    public void ScenePublisher_SendsFullSnapshotWhenLocationChanges()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        var scene = MakeScene();
        publisher.Publish(WorldId, PlayerId, scene, MakeStamp(1));
        events.Events.Clear();

        publisher.PublishIfChanged(
            WorldId,
            PlayerId,
            scene with
            {
                LocationId = Guid.NewGuid(),
            },
            MakeStamp(2)
        );

        Assert.IsType<SceneUpdatedEvent>(Assert.Single(events.Events));
    }

    [Fact]
    public void ScenePublisher_SendsOnlyFullSnapshotWhenUnsupportedAndSupportedFieldsChange()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        publisher.Publish(
            WorldId,
            PlayerId,
            MakeScene(weather: WeatherCondition.Clear),
            MakeStamp(1)
        );
        events.Events.Clear();

        publisher.PublishIfChanged(
            WorldId,
            PlayerId,
            MakeScene(weather: WeatherCondition.Rain, exits: [MakeExit(isLocked: false)]),
            MakeStamp(2)
        );

        Assert.IsType<SceneUpdatedEvent>(Assert.Single(events.Events));
    }

    [Fact]
    public void ScenePublisher_ReanchorsClockAfterTimeAdvanceWithoutOtherChanges()
    {
        var events = new RecordingGameClientEventSink();
        var publisher = new ScenePublisher(events, new PublishedSceneRegistry());
        var scene = MakeScene();
        publisher.Publish(WorldId, PlayerId, scene, MakeStamp(1));
        events.Events.Clear();

        publisher.PublishAfterTimeAdvance(PlayerId, scene, MakeStamp(2));

        Assert.IsType<ClockReanchoredEvent>(Assert.Single(events.Events));
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenTheScenesAreIdentical()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(creatures: [MakeCreature(VillagerId)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenOnlyTheClockAdvances()
    {
        // Arrange
        var previous = MakeScene(hour: 8);
        var current = MakeScene(hour: 9);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenNearbyCreatureVitalsChange()
    {
        // Arrange
        var previous = MakeScene(
            player: MakeCreature(PlayerId, currentHp: 10),
            creatures: [MakeCreature(VillagerId, currentHp: 10)]
        );
        var current = MakeScene(
            player: MakeCreature(PlayerId, currentHp: 15),
            creatures: [MakeCreature(VillagerId, currentHp: 15)]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenOnlyTheDepartureCountdownChanges()
    {
        // Arrange
        var previous = MakeScene(caravans: [MakeCaravan(minutesUntilDeparture: 30)]);
        var current = MakeScene(caravans: [MakeCaravan(minutesUntilDeparture: 29)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenCreaturesAreListedInADifferentOrder()
    {
        // Arrange
        var otherId = Guid.NewGuid();
        var previous = MakeScene(creatures: [MakeCreature(VillagerId), MakeCreature(otherId)]);
        var current = MakeScene(creatures: [MakeCreature(otherId), MakeCreature(VillagerId)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureArrives()
    {
        // Arrange
        var previous = MakeScene(creatures: []);
        var current = MakeScene(creatures: [MakeCreature(VillagerId)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureDeparts()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(creatures: []);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureChangesState()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(
            creatures: [MakeCreature(VillagerId, activity: CreatureActivity.Working)]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureJourneyChanges()
    {
        // Arrange
        var previous = MakeScene(
            creatures:
            [
                MakeCreature(VillagerId, journey: new SceneJourneyInfo("Walking", "Market")),
            ]
        );
        var current = MakeScene(
            creatures:
            [
                MakeCreature(VillagerId, journey: new SceneJourneyInfo("Walking", "Temple")),
            ]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureGainsADot()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(
            creatures: [MakeCreature(VillagerId, effects: MakeEffects(MakeDot("Ignite", 60)))]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureGainsACondition()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(
            creatures:
            [
                MakeCreature(VillagerId, effects: MakeConditionEffects(ConditionType.Stunned, 60)),
            ]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenTheSameEffectsAreRebuilt()
    {
        // Arrange
        var previous = MakeScene(
            creatures:
            [
                MakeCreature(
                    VillagerId,
                    effects: MakeEffects(MakeDot("Ignite", 60), MakeDot("Venom", 90))
                ),
            ]
        );
        var current = MakeScene(
            creatures:
            [
                MakeCreature(
                    VillagerId,
                    effects: MakeEffects(MakeDot("Venom", 90), MakeDot("Ignite", 60))
                ),
            ]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenTheSameWalkIsRebuilt()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId, walk: MakeWalk())]);
        var current = MakeScene(creatures: [MakeCreature(VillagerId, walk: MakeWalk())]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenTheWalkStartsAtAnotherTime()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId, walk: MakeWalk(0))]);
        var current = MakeScene(creatures: [MakeCreature(VillagerId, walk: MakeWalk(30))]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureStartsWalking()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(creatures: [MakeCreature(VillagerId, walk: MakeWalk())]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenTheWeatherChanges()
    {
        // Arrange
        var previous = MakeScene(weather: WeatherCondition.Clear);
        var current = MakeScene(weather: WeatherCondition.Rain);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenAnExitBecomesLocked()
    {
        // Arrange
        var previous = MakeScene(exits: [MakeExit(isLocked: false)]);
        var current = MakeScene(exits: [MakeExit(isLocked: true)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACaravanArrives()
    {
        // Arrange
        var previous = MakeScene(caravans: []);
        var current = MakeScene(caravans: [MakeCaravan(minutesUntilDeparture: 30)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenACreatureMovesWithinTheLocation()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(
            creatures: [MakeCreature(VillagerId, placement: new Placement(6, 4, 0))]
        );

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsFalse_WhenPlacementIsIdentical()
    {
        // Arrange
        var previous = MakeScene(creatures: [MakeCreature(VillagerId)]);
        var current = MakeScene(creatures: [MakeCreature(VillagerId)]);

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void HasPlayerVisibleChange_ReturnsTrue_WhenThePlayerArrivesAtAnotherPoint()
    {
        // Arrange
        var previous = MakeScene(player: MakeCreature(PlayerId));
        var current = MakeScene(player: MakeCreature(PlayerId, placement: new Placement(9, 3, 0)));

        // Act
        var changed = SceneSemanticComparer.HasPlayerVisibleChange(previous, current);

        // Assert
        Assert.True(changed);
    }

    private static SceneResult MakeScene(
        int hour = 8,
        SceneCreatureInfo? player = null,
        IReadOnlyCollection<SceneCreatureInfo>? creatures = null,
        IReadOnlyCollection<SceneExitInfo>? exits = null,
        IReadOnlyCollection<SceneCaravanInfo>? caravans = null,
        WeatherCondition? weather = null
    ) =>
        new(
            WorldId,
            LocationId,
            new SceneDateInfo(975, "Thawmoon", 1, "Stormday", hour),
            new SceneStateInfo("Northmarch", null),
            null,
            null,
            null,
            null,
            player ?? MakeCreature(PlayerId),
            exits ?? [],
            [],
            creatures ?? [],
            [],
            weather,
            caravans ?? [],
            new Footprint(10, 10)
        );

    private static WorldStateStamp MakeStamp(long version) =>
        new(version, GameClock.Epoch, DateTimeOffset.UnixEpoch, 1);

    private sealed class RecordingGameClientEventSink : IGameClientEventSink
    {
        public List<GameClientEvent> Events { get; } = [];

        public void Enqueue(GameClientEvent gameEvent) => Events.Add(gameEvent);
    }

    private static SceneCreatureInfo MakeCreature(
        Guid id,
        CreatureActivity? activity = null,
        int currentHp = 10,
        SceneJourneyInfo? journey = null,
        CreatureEffects? effects = null,
        Placement? placement = null,
        SceneCreatureWalk? walk = null
    ) =>
        new(
            Id: id,
            Name: "Villager",
            CreatureType: CreatureType.Human,
            Gender: Gender.Female,
            Profession: null,
            Level: 1,
            Age: 30,
            FactionNames: ["Guild"],
            Condition: CreatureCondition.Awake,
            Activity: activity,
            Posture: CreaturePosture.Standing,
            Movement: CreatureMovement.Stationary,
            IsSneaking: false,
            IsAlerted: false,
            IsRestrained: false,
            Reputation: 0,
            Gold: 5,
            CurrentHp: currentHp,
            MaximumHp: 20,
            CurrentAp: 5,
            MaximumAp: 10,
            CurrentMp: 5,
            MaximumMp: 10,
            ExperienceCurrent: 0,
            ExperienceToNextLevel: 100,
            Strength: 1,
            Dexterity: 1,
            Intelligence: 1,
            Endurance: 1,
            Stamina: 1,
            Mana: 1,
            Defense: 1,
            MovementSpeed: 5,
            PhysicalResistance: 0,
            FireResistance: 0,
            IceResistance: 0,
            LightningResistance: 0,
            PoisonResistance: 0,
            MagicResistance: 0,
            TradeWorkstationId: null,
            QuestMarkers: [],
            ReadyToDeliver: false,
            Effects: effects ?? CreatureEffects.None,
            Journey: journey,
            Placement: placement ?? new Placement(id == PlayerId ? 1 : 2, id == PlayerId ? 3 : 4, 0)
        )
        {
            Walk = walk,
        };

    private static SceneCreatureWalk MakeWalk(int startedAtSecond = 0) =>
        new([new Point(0, 0), new Point(10, 0)], MakeInstant(startedAtSecond), 1.5, false);

    private static CreatureDotEffect MakeDot(string abilityName, int expiresAtSecond) =>
        new(abilityName, 3, DamageType.Fire, MakeInstant(expiresAtSecond));

    private static CreatureEffects MakeEffects(params CreatureDotEffect[] dots) =>
        CreatureEffects.None with
        {
            Dots = dots,
        };

    private static CreatureEffects MakeConditionEffects(
        ConditionType condition,
        int expiresAtSecond
    ) =>
        CreatureEffects.None with
        {
            Conditions = new Dictionary<ConditionType, GameInstant>
            {
                [condition] = MakeInstant(expiresAtSecond),
            },
        };

    private static GameInstant MakeInstant(int second) =>
        new(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(second));

    private static SceneExitInfo MakeExit(bool isLocked) =>
        new(
            ConnectorId: ConnectorId,
            Description: "A door.",
            Destination: new SceneWildernessExitDestination("Outside"),
            IsLocked: isLocked,
            Direction: null,
            IsVisited: false,
            IsWayBack: false,
            DestinationLocationId: DestinationId,
            Placement: new Placement(0, 0, 0)
        );

    private static SceneCaravanInfo MakeCaravan(int minutesUntilDeparture) =>
        new(
            CaravanId: CaravanId,
            RouteName: "The Capital Circuit",
            TicketFeeGold: 10,
            MinutesUntilDeparture: minutesUntilDeparture,
            PassengerServiceAvailable: true,
            Destinations: [new SceneCaravanDestination(DestinationId, "Capital", 4, false)]
        );
}
