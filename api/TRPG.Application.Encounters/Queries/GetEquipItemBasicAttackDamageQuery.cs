using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetEquipItemBasicAttackDamageQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class GetEquipItemBasicAttackDamageQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
    IQueryHandler<
        GetWeaponProficienciesQuery,
        IReadOnlyDictionary<WeaponType, int>
    > getAllWeaponProficiencies,
    DamageCalculator damageCalculator,
    IOptionsSnapshot<CombatOptions> optionsSnapshot
) : IQueryHandler<GetEquipItemBasicAttackDamageQuery, float>
{
    public async Task<float> Handle(
        GetEquipItemBasicAttackDamageQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = query.CreatureId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Creature {query.CreatureId} not found.");

        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(query.CreatureId, OwnerType.Creature),
            },
            cancellationToken
        );

        var toEquip = items.First(i => i.Id == query.ItemId);
        var currentlyEquipped = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        var conflicting = EquipmentLoadoutPolicy.GetConflictingItems(
            toEquip,
            query.Slot,
            currentlyEquipped
        );

        foreach (var conflictingItem in conflicting)
        {
            conflictingItem.Ownership.EquippedSlot = null;
        }
        toEquip.Ownership.EquippedSlot = EquipmentLoadoutPolicy.ResolveEquippedSlot(
            toEquip,
            query.Slot
        );

        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = query.CreatureId },
            cancellationToken
        );
        var weaponProficiencies = await getAllWeaponProficiencies.Handle(
            new GetWeaponProficienciesQuery
            {
                WorldId = creature.WorldId,
                CreatureId = query.CreatureId,
            },
            cancellationToken
        );

        var combatant = Combatant.FromCreature(
            optionsSnapshot.Value,
            isPlayer: true,
            creature,
            abilities,
            items,
            weaponProficiencies
        );

        return damageCalculator.EstimateBasicAttackDamagePerTurn(combatant);
    }
}
