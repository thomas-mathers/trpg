using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Handling;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

public class AllocateAttributePointsCommand
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyDictionary<AllocatableAttributeName, int> Deltas { get; init; }
}

public class AllocateAttributePointsCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetInventoryByOwnerQuery, IReadOnlyList<Item>> getInventoryByOwner,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
) : ICommandHandler<AllocateAttributePointsCommand>
{
    public async Task Handle(
        AllocateAttributePointsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Deltas.Count == 0)
        {
            return;
        }

        if (command.Deltas.Values.Any(delta => delta < 0))
        {
            throw new InvalidOperationException("Attribute point deltas cannot be negative.");
        }

        var creature = await context.Creatures.FirstAsync(
            c => c.Id == command.CreatureId,
            cancellationToken
        );

        var unallocated = StatFormulas.CalculateUnallocatedAttributePoints(
            creature.BaseAttributes,
            creature.Level,
            optionsSnapshot.Value
        );

        var requestedTotal = command.Deltas.Values.Sum();
        if (requestedTotal > unallocated)
        {
            throw new InvalidOperationException(
                $"Requested {requestedTotal} attribute points but only {unallocated} are available."
            );
        }

        foreach (var (attribute, delta) in command.Deltas)
        {
            ApplyDelta(creature.BaseAttributes, attribute, delta);
        }

        creature.BaseAttributes.MaximumHp = StatFormulas.CalculateMaximumHp(
            creature.BaseAttributes,
            optionsSnapshot.Value
        );
        creature.BaseAttributes.MaximumAp = StatFormulas.CalculateMaximumAp(
            creature.BaseAttributes,
            optionsSnapshot.Value
        );
        creature.BaseAttributes.MaximumMp = StatFormulas.CalculateMaximumMp(
            creature.BaseAttributes,
            optionsSnapshot.Value
        );

        var items = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(command.CreatureId, OwnerType.Creature),
            },
            cancellationToken
        );
        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyDelta(
        Attributes attributes,
        AllocatableAttributeName attribute,
        int delta
    )
    {
        switch (attribute)
        {
            case AllocatableAttributeName.Strength:
                attributes.Strength += delta;
                break;
            case AllocatableAttributeName.Dexterity:
                attributes.Dexterity += delta;
                break;
            case AllocatableAttributeName.Endurance:
                attributes.Endurance += delta;
                break;
            case AllocatableAttributeName.Stamina:
                attributes.Stamina += delta;
                break;
            case AllocatableAttributeName.Mana:
                attributes.Mana += delta;
                break;
            case AllocatableAttributeName.Intelligence:
                attributes.Intelligence += delta;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null);
        }
    }
}
