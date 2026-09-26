using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public class SceneSemanticComparerTests
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid VillagerId = Guid.NewGuid();
    private static readonly Guid CaravanId = Guid.NewGuid();
    private static readonly Guid DestinationId = Guid.NewGuid();

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
    public void HasPlayerVisibleChange_ReturnsFalse_WhenOnlyVitalMetersRegenerate()
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
        Assert.False(changed);
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
        var previous = MakeScene(creatures: [MakeCreature(VillagerId, state: CreatureState.Idle)]);
        var current = MakeScene(
            creatures: [MakeCreature(VillagerId, state: CreatureState.Working)]
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
            caravans ?? []
        );

    private static SceneCreatureInfo MakeCreature(
        Guid id,
        CreatureState state = CreatureState.Idle,
        int currentHp = 10,
        SceneJourneyInfo? journey = null
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
            State: state,
            IsSneaking: false,
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
            Journey: journey
        );

    private static SceneExitInfo MakeExit(bool isLocked) =>
        new(
            Description: "A door.",
            Destination: new SceneWildernessExitDestination("Outside"),
            IsLocked: isLocked,
            Direction: null,
            IsVisited: false,
            IsWayBack: false
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
