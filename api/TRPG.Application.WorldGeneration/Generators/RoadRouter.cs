using TRPG.Application.Common.Algorithms;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoadRouter
{
    private const double StepCost = 1;
    private const double BlockedStepPenalty = 25;
    private const double EdgeMarginPenalty = 6;
    private const double TurnPenalty = 1.5;
    private const int NoDirection = -1;
    private const int MinimumRun = 2;

    private static readonly RoadCell[] Steps = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    private static readonly RouteRules[] Attempts =
    [
        new(CrossBuildings: false, MinimumRun: MinimumRun),
        new(CrossBuildings: true, MinimumRun: MinimumRun),
        new(CrossBuildings: true, MinimumRun: 1),
    ];

    private readonly record struct RouteRules(bool CrossBuildings, int MinimumRun);

    private readonly record struct RoadState(RoadCell Cell, int Direction, int Run);

    private readonly record struct Neighbour(RoadState State, double Cost);

    internal static IReadOnlyList<RoadCell> Route(
        RoadGrid grid,
        RoadCell start,
        RoadCell heading,
        RoadNetworkCells network
    )
    {
        foreach (var rules in Attempts)
        {
            var route = Search(grid, start, heading, network, rules);

            if (route.Count > 0)
            {
                return route;
            }
        }

        return [start];
    }

    private static IReadOnlyList<RoadCell> Search(
        RoadGrid grid,
        RoadCell start,
        RoadCell heading,
        RoadNetworkCells network,
        RouteRules rules
    )
    {
        var first = new RoadState(start, Array.IndexOf(Steps, heading), 1);
        var states = Graphs.ShortestPathToNearest(
            [first],
            state => network.Contains(state.Cell),
            state => Neighbours(grid, start, rules, state).Select(neighbour => neighbour.State),
            (from, to) => Cost(grid, start, rules, from, to)
        );

        return states.Select(state => state.Cell).ToArray();
    }

    private static IEnumerable<Neighbour> Neighbours(
        RoadGrid grid,
        RoadCell start,
        RouteRules rules,
        RoadState state
    )
    {
        for (var direction = 0; direction < Steps.Length; direction++)
        {
            var cell = new RoadCell(
                state.Cell.Column + Steps[direction].Column,
                state.Cell.Row + Steps[direction].Row
            );

            if (
                !grid.Contains(cell)
                || !CanTurn(rules, state, direction)
                || !IsPassable(grid, start, rules, cell)
            )
            {
                continue;
            }

            var cost = StepCost;
            cost += grid.IsBlocked(cell) && cell != start ? BlockedStepPenalty : 0;
            cost += grid.IsDiscouraged(cell) ? EdgeMarginPenalty : 0;
            cost +=
                state.Direction != NoDirection && state.Direction != direction ? TurnPenalty : 0;

            var run = state.Direction == direction ? Math.Min(state.Run + 1, MinimumRun) : 1;

            yield return new Neighbour(new RoadState(cell, direction, run), cost);
        }
    }

    private static bool IsPassable(
        RoadGrid grid,
        RoadCell start,
        RouteRules rules,
        RoadCell cell
    ) => rules.CrossBuildings || !grid.IsBlocked(cell) || cell == start;

    private static bool CanTurn(RouteRules rules, RoadState state, int direction) =>
        state.Direction == NoDirection
        || state.Direction == direction
        || state.Run >= rules.MinimumRun;

    private static double Cost(
        RoadGrid grid,
        RoadCell start,
        RouteRules rules,
        RoadState from,
        RoadState to
    ) => Neighbours(grid, start, rules, from).Single(neighbour => neighbour.State == to).Cost;
}
