using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Domain;

public class RouteCycleTests
{
    private const int LingerHours = 2;
    private const float SpeedUnitsPerHour = 5;

    // Leg hours (distance / speed): stop0->stop1 = 2, stop1->stop2 = 4, stop2->stop3 = 1,
    // stop3->stop0 = 3. Total cycle = 4 * lingerHours + (2 + 4 + 1 + 3) = 18.
    private readonly Guid _stop0 = Guid.NewGuid();
    private readonly Guid _stop1 = Guid.NewGuid();
    private readonly Guid _stop2 = Guid.NewGuid();
    private readonly Guid _stop3 = Guid.NewGuid();
    private readonly List<RouteWaypoint> _stops;

    public RouteCycleTests()
    {
        _stops =
        [
            new RouteWaypoint(_stop0, DistanceToNextStop: 10),
            new RouteWaypoint(_stop1, DistanceToNextStop: 20),
            new RouteWaypoint(_stop2, DistanceToNextStop: 5),
            new RouteWaypoint(_stop3, DistanceToNextStop: 15),
        ];
    }

    [Fact]
    public void TotalCycleHours_SumsLingerAndLegHours_ForEveryStop()
    {
        var totalCycleHours = RouteCycle.TotalCycleHours(_stops, LingerHours, SpeedUnitsPerHour);

        Assert.Equal(18, totalCycleHours);
    }

    [Fact]
    public void Resolve_ReturnsLingeringAtFirstStop_AtCycleStart()
    {
        var position = RouteCycle.Resolve(_stops, LingerHours, SpeedUnitsPerHour, elapsedHours: 0);

        var lingering = Assert.IsType<RoutePosition.Lingering>(position);
        Assert.Equal(_stop0, lingering.LocationId);
        Assert.Equal(0, lingering.StopIndex);
        Assert.Equal(2, lingering.HoursUntilDeparture);
    }

    [Fact]
    public void Resolve_ReturnsLingeringWithOneHourLeft_JustBeforeDeparture()
    {
        var position = RouteCycle.Resolve(_stops, LingerHours, SpeedUnitsPerHour, elapsedHours: 1);

        var lingering = Assert.IsType<RoutePosition.Lingering>(position);
        Assert.Equal(_stop0, lingering.LocationId);
        Assert.Equal(1, lingering.HoursUntilDeparture);
    }

    [Fact]
    public void Resolve_ReturnsInTransit_AtTheExactDepartureInstant()
    {
        var position = RouteCycle.Resolve(_stops, LingerHours, SpeedUnitsPerHour, elapsedHours: 2);

        var inTransit = Assert.IsType<RoutePosition.InTransit>(position);
        Assert.Equal(_stop0, inTransit.FromLocationId);
        Assert.Equal(_stop1, inTransit.ToLocationId);
        Assert.Equal(2, inTransit.HoursUntilArrival);
    }

    [Fact]
    public void Resolve_ReturnsInTransit_MidLeg()
    {
        var position = RouteCycle.Resolve(_stops, LingerHours, SpeedUnitsPerHour, elapsedHours: 3);

        var inTransit = Assert.IsType<RoutePosition.InTransit>(position);
        Assert.Equal(_stop0, inTransit.FromLocationId);
        Assert.Equal(_stop1, inTransit.ToLocationId);
        Assert.Equal(1, inTransit.HoursUntilArrival);
    }

    [Fact]
    public void Resolve_ReturnsInTransitAcrossTheWraparoundLeg_JustBeforeCycleEnd()
    {
        var position = RouteCycle.Resolve(_stops, LingerHours, SpeedUnitsPerHour, elapsedHours: 17);

        var inTransit = Assert.IsType<RoutePosition.InTransit>(position);
        Assert.Equal(_stop3, inTransit.FromLocationId);
        Assert.Equal(_stop0, inTransit.ToLocationId);
        Assert.Equal(1, inTransit.HoursUntilArrival);
    }

    [Fact]
    public void Resolve_WrapsBackToFirstStop_AtExactlyOneFullCycle()
    {
        var position = RouteCycle.Resolve(_stops, LingerHours, SpeedUnitsPerHour, elapsedHours: 18);

        var lingering = Assert.IsType<RoutePosition.Lingering>(position);
        Assert.Equal(_stop0, lingering.LocationId);
        Assert.Equal(0, lingering.StopIndex);
    }

    [Fact]
    public void Resolve_IsEquivalentAcrossMultipleCycles_ViaModulo()
    {
        var position = RouteCycle.Resolve(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            elapsedHours: 18 + 5
        );

        var lingering = Assert.IsType<RoutePosition.Lingering>(position);
        Assert.Equal(_stop1, lingering.LocationId);
        Assert.Equal(1, lingering.StopIndex);
        Assert.Equal(1, lingering.HoursUntilDeparture);
    }

    [Fact]
    public void HoursBetween_ReturnsJustTheLegHours_ForAdjacentStops()
    {
        var hours = RouteCycle.HoursBetween(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            fromStopIndex: 0,
            toStopIndex: 1
        );

        Assert.Equal(2, hours);
    }

    [Fact]
    public void HoursBetween_IncludesIntermediateLingerHours_AcrossTheWraparound()
    {
        var hours = RouteCycle.HoursBetween(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            fromStopIndex: 2,
            toStopIndex: 0
        );

        Assert.Equal(6, hours);
    }

    [Fact]
    public void HoursUntilNextArrivalAt_ReturnsZero_WhenAlreadyLingeringAtTheTargetStop()
    {
        var hours = RouteCycle.HoursUntilNextArrivalAt(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            elapsedHours: 0,
            targetStopIndex: 0
        );

        Assert.Equal(0, hours);
    }

    [Fact]
    public void HoursUntilNextArrivalAt_ReturnsZero_MidwayThroughTheTargetStopsOwnLinger()
    {
        var hours = RouteCycle.HoursUntilNextArrivalAt(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            elapsedHours: 1,
            targetStopIndex: 0
        );

        Assert.Equal(0, hours);
    }

    [Fact]
    public void HoursUntilNextArrivalAt_ReturnsTheForwardDistance_ToAnUpcomingStop()
    {
        // Stop 1's linger begins at cumulative hour 4 (2 linger + 2 leg hours past stop 0).
        var hours = RouteCycle.HoursUntilNextArrivalAt(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            elapsedHours: 0,
            targetStopIndex: 1
        );

        Assert.Equal(4, hours);
    }

    [Fact]
    public void HoursUntilNextArrivalAt_WrapsAroundTheLoop_WhenThisCyclesWindowAlreadyPassed()
    {
        // Stop 1's linger window is [4, 6); at hour 7 it has already closed, so the next
        // occurrence is a full cycle (18 hours) later, minus the 3 hours since it began.
        var hours = RouteCycle.HoursUntilNextArrivalAt(
            _stops,
            LingerHours,
            SpeedUnitsPerHour,
            elapsedHours: 7,
            targetStopIndex: 1
        );

        Assert.Equal(15, hours);
    }

    [Fact]
    public void ToTravelOrder_ReturnsTheSameStops_ForClockwise()
    {
        var travelOrder = RouteCycle.ToTravelOrder(_stops, RouteDirection.Clockwise);

        Assert.Same(_stops, travelOrder);
    }

    [Fact]
    public void ToTravelOrder_WalksStopsBackward_RealigningEachLegsDistance_ForCounterClockwise()
    {
        var travelOrder = RouteCycle.ToTravelOrder(_stops, RouteDirection.CounterClockwise);

        // Same physical legs, walked in reverse: stop0's leg back to stop3 reuses stop3's original
        // (forward) distance to stop0, stop3's leg back to stop2 reuses stop2's original distance
        // to stop3, and so on.
        Assert.Equal([_stop0, _stop3, _stop2, _stop1], travelOrder.Select(stop => stop.LocationId));
        Assert.Equal([15f, 5f, 20f, 10f], travelOrder.Select(stop => stop.DistanceToNextStop));
    }
}
