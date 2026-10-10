using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

internal static class LocalMoveWalker
{
    public static void Advance(
        SimulatedCreature creature,
        GameInstant until,
        ICollection<SimEvent> events
    )
    {
        var move = creature.LocalMove!;
        var available =
            Math.Max(0, (until - creature.LastUpdate).TotalSeconds) * creature.MetersPerGameSecond;
        var walked = Math.Min(available, move.RemainingMeters);
        move.WalkedMeters += walked;
        creature.LastUpdate = until;
        if (move.RemainingMeters > 1e-9)
        {
            creature.NextUpdate = creature.TimeToLocalMoveEnd(until);
            return;
        }

        var completedAt =
            until - TimeSpan.FromSeconds((available - walked) / creature.MetersPerGameSecond);
        var stop = move.Plan.Path[^1];
        events.Add(
            new LocalMoveCompleted(creature.Id, completedAt, creature.LocationId, move.Plan, stop)
        );
        creature.LocalMove = null;
        creature.IsWalking = false;
        creature.NextUpdate = until;
    }

    public static Point Position(LocalMoveExecution move)
    {
        var remaining = move.WalkedMeters;
        foreach (var pair in move.Plan.Path.Zip(move.Plan.Path.Skip(1)))
        {
            var length = Distance(pair.First, pair.Second);
            if (remaining <= length)
            {
                var fraction = length == 0 ? 1 : remaining / length;
                return new Point(
                    pair.First.X + (pair.Second.X - pair.First.X) * fraction,
                    pair.First.Y + (pair.Second.Y - pair.First.Y) * fraction
                );
            }
            remaining -= length;
        }

        return move.Plan.Path[^1];
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));
}
