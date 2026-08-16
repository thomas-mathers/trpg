using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

public class StartFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> EnemyCreatureIds { get; init; }
    public Guid? EncounterId { get; init; }
}

internal class StartFightCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetCombatantQuery, Combatant> getCombatant,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    IGameClientEventSink gameEvents
) : ICommandHandler<StartFightCommand, IReadOnlyList<Combatant>>
{
    public async Task<IReadOnlyList<Combatant>> Handle(
        StartFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var regeneratedCreatures = await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = command.SessionId,
                CreatureIds = [command.PlayerId, .. command.EnemyCreatureIds],
            },
            cancellationToken
        );

        var combatants = new List<Combatant>();

        foreach (var creature in regeneratedCreatures.Values)
        {
            var combatant = await getCombatant.Handle(
                new GetCombatantQuery
                {
                    Creature = creature,
                    IsPlayer = creature.Id == command.PlayerId,
                },
                cancellationToken
            );
            combatants.Add(combatant);
        }

        context.Fights.Add(
            new Fight
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                CombatantIds = combatants.Select(c => c.CreatureId).ToList(),
                StartedAt = DateTime.UtcNow,
                EncounterId = command.EncounterId,
            }
        );
        await context.SaveChangesAsync(cancellationToken);
        gameEvents.Enqueue(new CombatStartedEvent(combatants));

        return combatants;
    }
}
