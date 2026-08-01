using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

internal class ResolveCombatRoundCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
    public required CombatState State { get; init; }
}

internal class ResolveCombatRoundCommandHandler(
    PersistCombatantsCommandHandler persistCombatants,
    AdjustWeaponProficienciesCommandHandler adjustWeaponProficiencies,
    AdjustCreatureSkillsCommandHandler adjustCreatureSkills,
    RemoveInventoryItemCommandHandler removeInventoryItem,
    EndFightCommandHandler endFight,
    GameTurnContext turnContext
)
{
    public async Task<CombatResult> Handle(
        ResolveCombatRoundCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var state = command.State;

        await persistCombatants.Handle(
            new PersistCombatantsCommand { Combatants = command.Combatants },
            cancellationToken
        );

        if (state.WeaponSwingCounts.Count > 0)
        {
            await adjustWeaponProficiencies.Handle(
                new AdjustWeaponProficienciesCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = command.PlayerId,
                    ProficiencyDeltas = state.WeaponSwingCounts,
                },
                cancellationToken
            );
        }

        if (state.SkillUsageCounts.Count > 0)
        {
            await adjustCreatureSkills.Handle(
                new AdjustCreatureSkillsCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = command.PlayerId,
                    UsageCounts = state.SkillUsageCounts,
                },
                cancellationToken
            );
        }

        foreach (var combatantState in state.Combatants)
        {
            foreach (var (itemId, quantity) in combatantState.ItemsUsedCounts)
            {
                await removeInventoryItem.Handle(
                    new RemoveInventoryItemCommand
                    {
                        CreatureId = combatantState.Id,
                        ItemId = itemId,
                        Quantity = quantity,
                    },
                    cancellationToken
                );
            }
        }

        if (state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled)
        {
            await endFight.Handle(
                new EndFightCommand
                {
                    SessionId = command.SessionId,
                    WorldId = command.WorldId,
                    State = state,
                },
                cancellationToken
            );
            turnContext.PendingEvents.Add(new CombatEndedEvent());
        }

        return state.ToCombatResult();
    }
}
