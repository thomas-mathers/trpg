using System.Transactions;
using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Events;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

public class ResolveCombatRoundCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
    public required CombatState State { get; init; }
}

public class ResolveCombatRoundCommandHandler(
    PersistCombatantsCommandHandler persistCombatants,
    AdjustWeaponProficienciesCommandHandler adjustWeaponProficiencies,
    AdjustCreatureSkillsCommandHandler adjustCreatureSkills,
    RemoveInventoryItemCommandHandler removeInventoryItem,
    EndFightCommandHandler endFight,
    IGameClientEventSink gameEvents,
    CreatureKilledQuestEventHandler creatureKilledQuestEvents
)
{
    public async Task<CombatResult> Handle(
        ResolveCombatRoundCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );
        var state = command.State;

        await persistCombatants.Handle(
            new PersistCombatantsCommand { Combatants = command.Combatants },
            cancellationToken
        );

        var isFightEnding =
            state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled;

        if (isFightEnding)
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
        }

        gameEvents.Enqueue(
            new CombatUpdatedEvent(
                CombatantStateMapper.ToCombatantStates(command.Combatants),
                CombatRoundEventMapper.ToCombatRoundEvents(state.Events),
                state.Outcome
            )
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

        foreach (
            var combatant in command.Combatants.Where(combatant =>
                combatant is { IsPlayer: false, IsAlive: false }
            )
        )
        {
            await creatureKilledQuestEvents.Handle(
                new CreatureKilledQuestEvent(
                    command.PlayerId,
                    command.WorldId,
                    combatant.CreatureId,
                    combatant.CreatureType
                ),
                cancellationToken
            );
        }

        transaction.Complete();
        return state.ToCombatResult();
    }
}
