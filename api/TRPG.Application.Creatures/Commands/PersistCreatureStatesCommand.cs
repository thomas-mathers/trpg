using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public record CreatureStateUpdate(
    Guid CreatureId,
    int CurrentHp,
    int CurrentAp,
    int CurrentMp,
    bool IsAlive,
    IReadOnlyDictionary<string, GameInstant> ActiveConditions,
    IReadOnlyDictionary<string, GameInstant> CooldownReadyAtByAbility,
    IReadOnlyList<ActiveDot> ActiveDots,
    IReadOnlyList<ActiveHot> ActiveHots,
    IReadOnlyList<ActiveBuff> ActiveBuffs
);

public class PersistCreatureStatesCommand
{
    public required IReadOnlyList<CreatureStateUpdate> Updates { get; init; }
}

internal class PersistCreatureStatesCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreatureEquipmentChangedEvent> creatureEquipmentChanged,
    IDomainEventPublisher<CreaturesDiedEvent> creaturesDied
) : ICommandHandler<PersistCreatureStatesCommand>
{
    public async Task Handle(
        PersistCreatureStatesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ids = command.Updates.Select(update => update.CreatureId).ToArray();
        var creatures = await context
            .Creatures.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var diedIds = new List<Guid>();

        foreach (var update in command.Updates)
        {
            var creature = creatures[update.CreatureId];
            creature.CurrentHp = update.CurrentHp;
            creature.CurrentAp = update.CurrentAp;
            creature.CurrentMp = update.CurrentMp;
            creature.ActiveConditions = new Dictionary<string, GameInstant>(
                update.ActiveConditions
            );
            creature.CooldownReadyAtByAbility = new Dictionary<string, GameInstant>(
                update.CooldownReadyAtByAbility
            );
            creature.ActiveDots = update.ActiveDots.ToList();
            creature.ActiveHots = update.ActiveHots.ToList();
            creature.ActiveBuffs = update.ActiveBuffs.ToList();

            if (!update.IsAlive)
            {
                if (creature.Condition != CreatureCondition.Dead)
                {
                    diedIds.Add(creature.Id);
                }

                creature.Die();
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (diedIds.Count > 0)
        {
            foreach (var world in diedIds.GroupBy(id => creatures[id].WorldId))
            {
                await creaturesDied.Publish(
                    new CreaturesDiedEvent(world.Key, [.. world]),
                    cancellationToken
                );
            }
        }

        foreach (var creatureId in ids)
        {
            await creatureEquipmentChanged.Publish(
                new CreatureEquipmentChangedEvent(creatureId),
                cancellationToken
            );
        }
    }
}
