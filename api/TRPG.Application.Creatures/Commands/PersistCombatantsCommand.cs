using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public record CreatureCombatStateUpdate(
    Guid CreatureId,
    int CurrentHp,
    int CurrentAp,
    int CurrentMp,
    bool IsAlive,
    IReadOnlyDictionary<string, int> ActiveConditions,
    IReadOnlyDictionary<string, int> CooldownRemainingByAbility,
    IReadOnlyList<ActiveDot> ActiveDots,
    IReadOnlyList<ActiveHot> ActiveHots,
    IReadOnlyList<ActiveBuff> ActiveBuffs
);

public class PersistCombatantsCommand
{
    public required IReadOnlyList<CreatureCombatStateUpdate> Updates { get; init; }
}

internal class PersistCombatantsCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreatureEquipmentChangedEvent> creatureEquipmentChanged
) : ICommandHandler<PersistCombatantsCommand>
{
    public async Task Handle(
        PersistCombatantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ids = command.Updates.Select(update => update.CreatureId).ToArray();
        var creatures = await context
            .Creatures.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var update in command.Updates)
        {
            var creature = creatures[update.CreatureId];
            creature.CurrentHp = update.CurrentHp;
            creature.CurrentAp = update.CurrentAp;
            creature.CurrentMp = update.CurrentMp;
            creature.ActiveConditions = new Dictionary<string, int>(update.ActiveConditions);
            creature.CooldownRemainingByAbility = new Dictionary<string, int>(
                update.CooldownRemainingByAbility
            );
            creature.ActiveDots = update.ActiveDots.ToList();
            creature.ActiveHots = update.ActiveHots.ToList();
            creature.ActiveBuffs = update.ActiveBuffs.ToList();

            if (!update.IsAlive)
            {
                creature.State = CreatureState.Dead;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var creatureId in ids)
        {
            await creatureEquipmentChanged.Publish(
                new CreatureEquipmentChangedEvent(creatureId),
                cancellationToken
            );
        }
    }
}
