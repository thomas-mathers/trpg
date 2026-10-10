using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Arrivals;

public class ExecuteJourneyArrivalsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<JourneyCompleted> Arrivals { get; init; }
}

public sealed record JourneyArrivalResult(IReadOnlyCollection<LocalMovePlan> LocalMoves);

internal sealed class ExecuteJourneyArrivalsCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    LocalActivityPlanner planner,
    LocalActivityCompleter completer
) : ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult>
{
    public async Task<JourneyArrivalResult> Handle(
        ExecuteJourneyArrivalsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = [.. command.Arrivals.Select(arrival => arrival.CreatureId)],
            },
            cancellationToken
        );
        var moves = new List<LocalMovePlan>();
        foreach (var arrival in command.Arrivals.OrderBy(arrival => arrival.At))
        {
            if (
                creatures.GetValueOrDefault(arrival.CreatureId) is not { } creature
                || creature.LocationId != arrival.LocationId
            )
            {
                continue;
            }

            var job = new CreatureJob
            {
                Id = arrival.JobId,
                CreatureId = creature.Id,
                WorldId = creature.WorldId,
                LocationId = arrival.LocationId,
                Action = arrival.Action,
            };
            var origin = arrival.StopPosition ?? new Point(creature.X, creature.Y);
            var move = await planner.Plan(creature, job, origin, cancellationToken);
            if (move is null)
            {
                await completer.CompleteStanding(creature, job, cancellationToken);
            }
            else
            {
                moves.Add(move);
            }
        }

        return new JourneyArrivalResult(moves);
    }
}
