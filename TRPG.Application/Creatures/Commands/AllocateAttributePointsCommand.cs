using Microsoft.EntityFrameworkCore;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class AllocateAttributePointsCommand
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyDictionary<AttributeName, int> Deltas { get; init; }
}

internal class AllocateAttributePointsCommandHandler(
    TrpgDbContext context,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
    StatFormulas statFormulas
)
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

        var unallocated = statFormulas.CalculateUnallocatedAttributePoints(
            creature.BaseAttributes,
            creature.Level
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

        creature.BaseAttributes.MaximumHp = statFormulas.CalculateMaximumHp(creature.BaseAttributes);
        creature.BaseAttributes.MaximumAp = statFormulas.CalculateMaximumAp(creature.BaseAttributes);
        creature.BaseAttributes.MaximumMp = statFormulas.CalculateMaximumMp(creature.BaseAttributes);

        var inventoryItems = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = command.CreatureId },
            cancellationToken
        );
        var equippedItems = inventoryItems
            .Where(i => i.EquippedSlot != null)
            .Select(i => i.Item)
            .ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyDelta(Attributes attributes, AttributeName attribute, int delta)
    {
        switch (attribute)
        {
            case AttributeName.Strength:
                attributes.Strength += delta;
                break;
            case AttributeName.Dexterity:
                attributes.Dexterity += delta;
                break;
            case AttributeName.Endurance:
                attributes.Endurance += delta;
                break;
            case AttributeName.Stamina:
                attributes.Stamina += delta;
                break;
            case AttributeName.Mana:
                attributes.Mana += delta;
                break;
            case AttributeName.Intelligence:
                attributes.Intelligence += delta;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(attribute),
                    attribute,
                    "Attribute is not allocatable."
                );
        }
    }
}
