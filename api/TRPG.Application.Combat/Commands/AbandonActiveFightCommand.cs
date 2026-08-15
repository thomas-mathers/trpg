using Microsoft.EntityFrameworkCore;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

public class AbandonActiveFightCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class AbandonActiveFightCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetActiveFightQuery, Fight?> getActiveFight,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
) : ICommandHandler<AbandonActiveFightCommand>
{
    public async Task Handle(
        AbandonActiveFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );

        if (fight == null)
        {
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = fight.CombatantIds },
            cancellationToken
        );

        var survivingCreatureIds = creaturesById
            .Values.Where(c => c.State != CreatureState.Dead)
            .Select(c => c.Id)
            .ToArray();

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = survivingCreatureIds,
                LastRegenPlaytime = command.Playtime,
            },
            cancellationToken
        );

        await context
            .Fights.Where(f => f.Id == fight.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(f => f.CompletedAt, DateTime.UtcNow)
                        .SetProperty(f => f.Outcome, CombatOutcome.Fled),
                cancellationToken
            );

        await transaction.CommitAsync(cancellationToken);
    }
}
