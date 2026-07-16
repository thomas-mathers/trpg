using TRPG.Application.Creatures.Commands;
using TRPG.Application.Game.Queries;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data;

namespace TRPG.Application.Combat.Commands;

internal class EndCombatCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required CombatState State { get; init; }
}

internal class EndCombatCommandHandler(
    TrpgDbContext context,
    AdjustWeaponProficienciesCommandHandler adjustWeaponProficiencies,
    ApplyCombatRewardsCommandHandler applyCombatRewards,
    GetPlaytimeQueryHandler getPlaytime,
    PersistCombatantResourcesCommandHandler persistCombatantResources,
    ClearCombatantsCommandHandler clearCombatants
)
{
    public async Task Handle(EndCombatCommand command, CancellationToken cancellationToken = default)
    {
        var state = command.State;
        var playerId = state.Combatants.Single(c => c.IsPlayer).Id;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await adjustWeaponProficiencies.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = command.WorldId,
                CreatureId = playerId,
                ProficiencyDeltas = state.WeaponSwingCounts,
            },
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
        await persistCombatantResources.Handle(
            new PersistCombatantResourcesCommand { Combatants = state.Combatants, Playtime = playtime },
            cancellationToken
        );

        await clearCombatants.Handle(
            new ClearCombatantsCommand { SessionId = command.SessionId },
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
    }
}
