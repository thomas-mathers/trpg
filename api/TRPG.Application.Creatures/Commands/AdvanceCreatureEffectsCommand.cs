using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class AdvanceCreatureEffectsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class AdvanceCreatureEffectsCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetInventoryItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getInventoryByOwners,
    CreatureEffectAdvancer advancer,
    ICommandHandler<PersistCreatureStatesCommand> persistCreatureStates
) : ICommandHandler<AdvanceCreatureEffectsCommand, IReadOnlyCollection<CreatureVitals>>
{
    public async Task<IReadOnlyCollection<CreatureVitals>> Handle(
        AdvanceCreatureEffectsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatures = await LoadAffectedCreatures(command, cancellationToken);
        if (creatures.Length == 0)
        {
            return [];
        }

        var results = await AdvanceEffects(creatures, command.GameTime, cancellationToken);
        if (results.Length == 0)
        {
            return [];
        }

        await persistCreatureStates.Handle(
            new PersistCreatureStatesCommand
            {
                Updates = results.Select(result => result.Update).ToArray(),
            },
            cancellationToken
        );
        return results.Select(result => result.Vitals).ToArray();
    }

    private async Task<Creature[]> LoadAffectedCreatures(
        AdvanceCreatureEffectsCommand command,
        CancellationToken cancellationToken
    )
    {
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = command.CreatureIds },
            cancellationToken
        );
        return creaturesById
            .Values.Where(creature =>
                creature.Condition != CreatureCondition.Dead && creature.HasActiveEffects
            )
            .ToArray();
    }

    private async Task<AdvancedCreatureEffects[]> AdvanceEffects(
        IReadOnlyCollection<Creature> creatures,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var items = await getInventoryByOwners.Handle(
            new GetInventoryItemsByOwnersQuery
            {
                CreatureIds = creatures.Select(creature => creature.Id).ToArray(),
            },
            cancellationToken
        );
        return creatures
            .Select(creature =>
                advancer.Advance(
                    creature,
                    items
                        .GetValueOrDefault(creature.Id, [])
                        .Where(item => item.Ownership.EquippedSlot != null)
                        .ToArray(),
                    now
                )
            )
            .OfType<AdvancedCreatureEffects>()
            .ToArray();
    }
}
