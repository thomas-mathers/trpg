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
    private static readonly TimeSpan Start = TimeSpan.FromHours(10);
    private const double SpeedUnitsPerHour = 10;

    private static readonly RouteTimelineStep[] FiniteSteps =
    [
        new(LocationA, ConnectorA, Distance: 5, DwellHours: 0.25),
        new(LocationB, ConnectorB, Distance: 10, DwellHours: 0),
        new(LocationC, ConnectorId: null, Distance: 0, DwellHours: 0),
    ];

    [Fact]
    public void Resolve_ReturnsPending_WhenPlaytimePrecedesStart()
    {
        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            Start - GameClock.RealTimePerInGameHour * 0.5
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
        var playtime = Start + GameClock.RealTimePerInGameHour * 0.5;

        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            playtime
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
        var expectedArrival = Start + GameClock.RealTimePerInGameHour * 1.75;

        var position = RouteTimeline.Resolve(
            FiniteSteps,
            RouteTraversal.Finite,
            SpeedUnitsPerHour,
            Start,
            expectedArrival + GameClock.RealTimePerInGameHour
        );

        var arrived = Assert.IsType<RouteTimelinePosition.Arrived>(position);
        Assert.Equal(LocationC, arrived.LocationId);
        Assert.Equal(2, arrived.StepIndex);
        Assert.Equal(expectedArrival, arrived.ArrivedAtPlaytime);
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
            Start + GameClock.RealTimePerInGameHour * cycleHours
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
}
