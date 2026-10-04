namespace TRPG.Application.Scenes.Roads;

internal static class RoadRouter
{
    private const double FreeStepCost = 1;
    private const double NetworkStepCost = 0.15;
    private const double BlockedStepPenalty = 25;
    private const double TurnPenalty = 0.4;
    private const int NoDirection = -1;

    private static readonly RoadCell[] Steps = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    private readonly record struct RoadState(RoadCell Cell, int Direction);

    private readonly record struct Neighbour(RoadState State, double Cost);

    internal static IReadOnlyList<RoadCell> Route(
        RoadGrid grid,
        RoadCell start,
        IReadOnlySet<RoadCell> network
    )
    {
        var first = new RoadState(start, NoDirection);
        var best = new Dictionary<RoadState, double> { [first] = 0 };
        var parent = new Dictionary<RoadState, RoadState>();
        var queue = new PriorityQueue<RoadState, double>();
        queue.Enqueue(first, 0);

        while (queue.TryDequeue(out var state, out var cost))
        {
            if (cost > best[state])
            {
                continue;
            }

            if (network.Contains(state.Cell))
            {
                return Trace(parent, state);
            }

            foreach (var next in Neighbours(grid, state, network))
            {
                var nextCost = cost + next.Cost;

                if (!best.TryGetValue(next.State, out var known) || nextCost < known)
                {
                    best[next.State] = nextCost;
                    parent[next.State] = state;
                    queue.Enqueue(next.State, nextCost);
                }
            }
        }

        return [start];
    }

    private static IEnumerable<Neighbour> Neighbours(
        RoadGrid grid,
        RoadState state,
        IReadOnlySet<RoadCell> network
    )
    {
        for (var direction = 0; direction < Steps.Length; direction++)
        {
            var cell = new RoadCell(
                state.Cell.Column + Steps[direction].Column,
                state.Cell.Row + Steps[direction].Row
            );

            if (!grid.Contains(cell))
            {
                continue;
            }

            var cost = network.Contains(cell) ? NetworkStepCost : FreeStepCost;
            cost += grid.IsBlocked(cell) ? BlockedStepPenalty : 0;
            cost +=
                state.Direction != NoDirection && state.Direction != direction ? TurnPenalty : 0;

            yield return new Neighbour(new RoadState(cell, direction), cost);
        }
    }

    private static IReadOnlyList<RoadCell> Trace(
        Dictionary<RoadState, RoadState> parent,
        RoadState goal
    )
    {
        var cells = new List<RoadCell> { goal.Cell };
        var current = goal;

        while (parent.TryGetValue(current, out var previous))
        {
            cells.Add(previous.Cell);
            current = previous;
        }

        cells.Reverse();

        return cells;
    }
}
