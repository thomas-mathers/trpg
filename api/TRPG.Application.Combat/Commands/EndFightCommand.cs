using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Reputations.Commands;
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
    ICommandHandler<AdjustReputationCommand> adjustReputation,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IOptionsMonitor<ReputationOptions> reputationOptions
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

        await ApplyKilledFactionReputation(state, cancellationToken);

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

    private async Task ApplyKilledFactionReputation(
        CombatState state,
        CancellationToken cancellationToken
    )
    {
        var player = state.Combatants.FirstOrDefault(c => c.IsPlayer);
        var killedCreatureIds = state
            .Combatants.Where(c => !c.IsPlayer && !c.IsAlive)
            .Select(c => c.Id)
            .ToArray();

        if (player == null || killedCreatureIds.Length == 0)
        {
            return;
        }

        var killedFactionIds = await context
            .FactionMembers.AsNoTracking()
            .Where(m => killedCreatureIds.Contains(m.CreatureId))
            .Select(m => m.FactionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        foreach (var factionId in killedFactionIds)
        {
            await adjustReputation.Handle(
                new AdjustReputationCommand
                {
                    CreatureId = player.Id,
                    TargetId = factionId,
                    TargetType = ReputationTargetType.Faction,
                    DeltaScore = reputationOptions.CurrentValue.KillReputationPenalty,
                    Reason = "Killed a member of this faction",
                },
                cancellationToken
            );
        }
    }
}
