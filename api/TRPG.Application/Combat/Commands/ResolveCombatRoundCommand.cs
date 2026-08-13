using System.Transactions;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests;
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
    public bool PublishEvents { get; init; } = true;
}

internal class ResolveCombatRoundCommandHandler(
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

        if (command.PublishEvents)
        {
            gameEvents.Enqueue(
                new CombatUpdatedEvent(
                    FightStateMapper.ToFightState(command.Combatants),
                    CombatRoundEventMapper.ToCombatRoundEvents(state.Events)
                )
            );
        }

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
            if (command.PublishEvents)
            {
                gameEvents.Enqueue(new CombatEndedEvent(state.Outcome));
            }
        }

        foreach (
            var combatant in command.Combatants.Where(combatant =>
                !combatant.IsPlayer && !combatant.IsAlive
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
