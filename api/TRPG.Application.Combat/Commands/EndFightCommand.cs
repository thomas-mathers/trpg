using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

internal class EndFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required CombatState State { get; init; }
}

internal class EndFightCommandHandler(
    TrpgDbContext context,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime
) : ICommandHandler<EndFightCommand>
{
    public async Task Handle(EndFightCommand command, CancellationToken cancellationToken = default)
    {
        var state = command.State;

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var survivingCreatureIds = state
            .Combatants.Where(c => c.IsAlive)
            .Select(c => c.Id)
            .ToArray();

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = survivingCreatureIds,
                LastRegenPlaytime = playtime,
            },
            cancellationToken
        );

        await context
            .Fights.Where(f => f.WorldId == command.WorldId && f.Outcome == CombatOutcome.Ongoing)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(f => f.CompletedAt, DateTime.UtcNow)
                        .SetProperty(f => f.Outcome, state.Outcome),
                cancellationToken
            );

        transaction.Complete();
    }
}
