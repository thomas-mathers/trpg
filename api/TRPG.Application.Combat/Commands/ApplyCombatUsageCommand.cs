using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Domain;

namespace TRPG.Application.Combat.Commands;

public class ApplyCombatUsageCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required CombatState State { get; init; }
}

internal class ApplyCombatUsageCommandHandler(
    ICommandHandler<AdjustWeaponProficienciesCommand> adjustWeaponProficiencies,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    ICommandHandler<RemoveInventoryItemsCommand> removeInventoryItems
) : ICommandHandler<ApplyCombatUsageCommand>
{
    public async Task Handle(
        ApplyCombatUsageCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var state = command.State;

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
                    GameTime = command.GameTime,
                    UsageCounts = state.SkillUsageCounts,
                },
                cancellationToken
            );
        }

        var itemRemovals = state
            .Combatants.SelectMany(combatantState =>
                combatantState.ItemsUsedCounts.Select(itemUsedCount => new InventoryItemRemoval(
                    combatantState.Id,
                    itemUsedCount.Key,
                    itemUsedCount.Value
                ))
            )
            .ToArray();

        if (itemRemovals.Length > 0)
        {
            await removeInventoryItems.Handle(
                new RemoveInventoryItemsCommand { Removals = itemRemovals },
                cancellationToken
            );
        }
    }
}
