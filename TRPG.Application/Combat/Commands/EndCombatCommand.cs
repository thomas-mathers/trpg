using TRPG.Application.Creatures.Commands;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data;
using TRPG.Data.Models;

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
    UpdateCreaturesCommandHandler updateCreatures,
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

            var enemyCreatureIds = state
                .Combatants.Where(c => !c.IsPlayer)
                .Select(c => c.Id)
                .ToArray();

            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = enemyCreatureIds,
                    State = CreatureState.Dead,
                },
                cancellationToken
            );
        }

        await clearCombatants.Handle(
            new ClearCombatantsCommand { SessionId = command.SessionId },
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
    }
}
