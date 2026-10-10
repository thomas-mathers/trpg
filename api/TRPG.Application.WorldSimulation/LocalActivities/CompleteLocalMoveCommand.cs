using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.LocalActivities;

public class CompleteLocalMoveCommand
{
    public required LocalMovePlan Move { get; init; }
    public required Point StopPosition { get; init; }
}

internal sealed class CompleteLocalMoveCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreature,
    LocalActivityPlanner planner,
    LocalActivityCompleter completer
) : ICommandHandler<CompleteLocalMoveCommand, LocalMovePlan?>
{
    public async Task<LocalMovePlan?> Handle(
        CompleteLocalMoveCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await getCreature.Handle(
            new GetCreatureByIdQuery { Id = command.Move.CreatureId },
            cancellationToken
        );
        if (creature is null || creature.LocationId != command.Move.LocationId)
        {
            return null;
        }
        if (await completer.CompleteAtTarget(creature, command.Move, cancellationToken))
        {
            return null;
        }

        var job = new CreatureJob
        {
            Id = command.Move.JobId,
            CreatureId = creature.Id,
            WorldId = creature.WorldId,
            LocationId = command.Move.LocationId,
            Action = command.Move.Action,
        };
        var retry = await planner.Plan(creature, job, command.StopPosition, cancellationToken);
        if (retry is not null)
        {
            return retry;
        }

        await completer.CompleteStanding(creature, job, cancellationToken);
        return null;
    }
}
