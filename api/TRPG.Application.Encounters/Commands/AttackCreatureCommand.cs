using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class AttackCreatureCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class AttackCreatureCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetEncounterGroupCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getEncounterGroupCreatureIds,
    ICommandHandler<WakeCreaturesCommand> wakeCreatures,
    ICommandHandler<AlertCreaturesCommand> alertCreatures,
    ICommandHandler<RecordAssaultCommand> recordAssault,
    ICommandHandler<StartFightCommand> startFight
) : ICommandHandler<AttackCreatureCommand>
{
    public async Task Handle(
        AttackCreatureCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        var target = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.TargetId },
            cancellationToken
        );

        var failure = FirstFailure(player, target);
        if (failure is not null)
            throw new InvalidOperationException(failure);

        var hasSurpriseRound =
            player!.IsSneaking || target!.Condition == CreatureCondition.Sleeping;

        var enemyCreatureIds = await getEncounterGroupCreatureIds.Handle(
            new GetEncounterGroupCreatureIdsQuery
            {
                WorldId = command.WorldId,
                CreatureId = command.TargetId,
            },
            cancellationToken
        );

        await wakeCreatures.Handle(
            new WakeCreaturesCommand { CreatureIds = enemyCreatureIds },
            cancellationToken
        );

        await alertCreatures.Handle(
            new AlertCreaturesCommand { CreatureIds = enemyCreatureIds },
            cancellationToken
        );

        // The player chose to attack, which is what separates assault from self-defence.
        await recordAssault.Handle(
            new RecordAssaultCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                VictimId = command.TargetId,
            },
            cancellationToken
        );

        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = enemyCreatureIds,
                HasSurpriseRound = hasSurpriseRound,
                PlayerWasAggressor = true,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private static string? FirstFailure(Creature? player, Creature? target)
    {
        if (player is null)
            return "The player does not exist.";
        if (target is null || target.LocationId != player.LocationId)
            return "That target is not here.";
        if (target.Id == player.Id)
            return "You can't attack yourself.";
        if (target.Condition == CreatureCondition.Dead)
            return $"{target.Name} is already dead.";
        if (target.IsRestrained)
            return $"{target.Name} is locked away and cannot be reached.";
        return null;
    }
}
