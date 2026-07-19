using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;

namespace TRPG.Application.Combat.Commands;

internal class EndFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required CombatState State { get; init; }
}

internal class EndFightCommandHandler(
    TrpgDbContext context,
    ApplyCombatRewardsCommandHandler applyCombatRewards,
    UpdateCreaturesCommandHandler updateCreatures,
    GetPlaytimeQueryHandler getPlaytime
)
{
    public async Task Handle(EndFightCommand command, CancellationToken cancellationToken = default)
    {
        var state = command.State;
        var playerId = state.Combatants.Single(c => c.IsPlayer).Id;

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        if (state.Outcome == CombatOutcome.Victory)
        {
            await applyCombatRewards.Handle(
                new ApplyCombatRewardsCommand
                {
                    CreatureId = playerId,
                    ExperienceGained = state.XpGained ?? 0,
                    GoldGained = state.GoldLooted ?? 0,
                },
                cancellationToken
            );
        }

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
            .Fights.Where(f => f.WorldId == command.WorldId && f.CompletedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(f => f.CompletedAt, DateTime.UtcNow),
                cancellationToken
            );

        await transaction.CommitAsync(cancellationToken);
    }
}
