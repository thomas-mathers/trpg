using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Domain;

public class RouteTimelineTests
{
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();
    private static readonly Guid LocationC = Guid.NewGuid();
    private static readonly Guid ConnectorA = Guid.NewGuid();
    private static readonly Guid ConnectorB = Guid.NewGuid();
    private static readonly GameInstant Start = GameClock.Epoch + TimeSpan.FromHours(10);
    private const double SpeedUnitsPerHour = 10;

    private static readonly RouteTimelineStep[] FiniteSteps =
    [
        new(LocationA, ConnectorA, Distance: 5, DwellHours: 0.25),
        new(LocationB, ConnectorB, Distance: 10, DwellHours: 0),
        new(LocationC, ConnectorId: null, Distance: 0, DwellHours: 0),
    ];

    [Fact]
    public void Resolve_ReturnsPending_WhenGameTimePrecedesStart()
    {
        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            Start - TimeSpan.FromHours(1) * 0.5
        );

        var pending = Assert.IsType<RouteTimelinePosition.Pending>(position);
        Assert.Equal(LocationA, pending.LocationId);
        Assert.Equal(0.5, pending.HoursUntilStart, precision: 10);
    }

    [Fact]
    public void Resolve_ReturnsLingering_AtStart()
    {
        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            Start
        );

        var lingering = Assert.IsType<RouteTimelinePosition.Lingering>(position);
        Assert.Equal(LocationA, lingering.LocationId);
        Assert.Equal(0.25, lingering.HoursUntilDeparture, precision: 10);
    }

    [Fact]
    public void Resolve_ReturnsInTransit_WithoutRoundingToAnHour()
    {
        var gameTime = Start + TimeSpan.FromHours(0.5);

        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            gameTime
        );

        var inTransit = Assert.IsType<RouteTimelinePosition.InTransit>(position);
        Assert.Equal(ConnectorA, inTransit.ConnectorId);
        Assert.Equal(LocationA, inTransit.FromLocationId);
        Assert.Equal(LocationB, inTransit.ToLocationId);
        Assert.Equal(0.25, inTransit.HoursUntilArrival, precision: 10);
    }

    [Fact]
    public void Resolve_ReturnsArrived_AtFiniteTerminal()
    {
        var expectedArrival = Start + TimeSpan.FromHours(1) * 1.75;

        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            expectedArrival + TimeSpan.FromHours(1)
        );

        var arrived = Assert.IsType<RouteTimelinePosition.Arrived>(position);
        Assert.Equal(LocationC, arrived.LocationId);
        Assert.Equal(2, arrived.StepIndex);
        Assert.Equal(expectedArrival, arrived.ArrivedAtGameTime);
    }

    [Fact]
    public void Resolve_WrapsAtExactCyclicBoundary()
    {
        RouteTimelineStep[] steps =
        [
            new(LocationA, ConnectorA, Distance: 5, DwellHours: 0.5),
            new(LocationB, ConnectorB, Distance: 5, DwellHours: 0.5),
        ];
        var cycleHours = RouteTimeline.TotalDurationHours(
            steps,
            RouteTraversal.Cyclic,
            SpeedUnitsPerHour
        );

        var position = RouteTimeline.Resolve(
            steps,
            RouteTraversal.Cyclic,
            SpeedUnitsPerHour,
            Start,
            Start + TimeSpan.FromHours(1) * cycleHours
        );

        var lingering = Assert.IsType<RouteTimelinePosition.Lingering>(position);
        Assert.Equal(LocationA, lingering.LocationId);
        Assert.Equal(0.5, lingering.HoursUntilDeparture, precision: 10);
    }

    [Fact]
    public void Resolve_RejectsFiniteRouteWithoutTerminalStep()
    {
        RouteTimelineStep[] steps = [new(LocationA, ConnectorA, Distance: 5, DwellHours: 0)];

        var exception = Assert.Throws<ArgumentException>(() =>
            RouteTimeline.Resolve(steps, RouteTraversal.Finite, SpeedUnitsPerHour, Start, Start)
        );

        Assert.Contains("terminal step", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HoursBetween_IncludesIntermediateDwellButNotEndpointDwell()
    {
        RouteTimelineStep[] steps =
        [
            new(LocationA, ConnectorA, Distance: 5, DwellHours: 0.25),
            new(LocationB, ConnectorB, Distance: 10, DwellHours: 0.5),
            new(LocationC, Guid.NewGuid(), Distance: 5, DwellHours: 0.75),
        ];

        var hours = RouteTimeline.HoursBetween(
            steps,
            SpeedUnitsPerHour,
            fromStepIndex: 0,
            toStepIndex: 2
        );

        Assert.Equal(2, hours, precision: 10);
    }

    [Fact]
    public void HoursUntilNextArrivalAt_UsesTheSharedStartTime()
    {
        RouteTimelineStep[] steps =
        [
            new(LocationA, ConnectorA, Distance: 5, DwellHours: 0.5),
            new(LocationB, ConnectorB, Distance: 5, DwellHours: 0.5),
        ];
        var gameTime = Start + TimeSpan.FromHours(1.25);

        var hours = RouteTimeline.HoursUntilNextArrivalAt(
            steps,
            SpeedUnitsPerHour,
            Start,
            gameTime,
            targetStepIndex: 0
        );

        Assert.Equal(0.75, hours, precision: 10);
    }
}
