using TRPG.Application.Combat.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

internal class StartFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string TargetName { get; init; }
}

internal class StartFightCommandHandler(
    TrpgDbContext context,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreaturesAtLocationQueryHandler getCreaturesAtLocation,
    GetCombatantQueryHandler getCombatant,
    ApplyPassiveRegenCommandHandler applyPassiveRegen
)
{
    private static readonly IReadOnlyCollection<CreatureType> HostileCreatureTypes =
        Enum.GetValues<CreatureType>().Except(CreatureTypes.Humanoid).ToArray();

    public async Task<IReadOnlyList<Combatant>> Handle(
        StartFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = player!.WorldId,
                LocationId = player.LocationId!.Value,
                ExcludingCreatureId = player.Id,
                CreatureTypes = HostileCreatureTypes,
                IncludeDead = false,
            },
            cancellationToken
        );

        if (nearby.Count == 0)
        {
            throw new InvalidOperationException("There's nothing here to attack.");
        }

        if (nearby.All(c => c.Name != command.TargetName))
        {
            throw new InvalidOperationException(
                $"No '{command.TargetName}' found nearby to attack. Call look to see what's around."
            );
        }

        var enemyIds = nearby.Select(summary => summary.Id).ToArray();

        var regeneratedCreatures = await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = command.SessionId,
                CreatureIds = [player.Id, .. enemyIds],
            },
            cancellationToken
        );

        var combatants = new List<Combatant>();

        foreach (var creature in regeneratedCreatures.Values)
        {
            var combatant = await getCombatant.Handle(
                new GetCombatantQuery { Creature = creature, IsPlayer = creature.Id == player.Id },
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
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        return combatants;
    }
}
