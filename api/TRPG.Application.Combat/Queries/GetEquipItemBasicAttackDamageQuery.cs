using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Queries;

public class PreviewEquipItemBasicAttackDamageQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class PreviewEquipItemBasicAttackDamageQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
    IQueryHandler<
        GetAllWeaponProficienciesQuery,
        IReadOnlyDictionary<WeaponType, int>
    > getAllWeaponProficiencies,
    DamageCalculator damageCalculator,
    IOptionsSnapshot<CombatOptions> optionsSnapshot
) : IQueryHandler<PreviewEquipItemBasicAttackDamageQuery, float>
{
    public async Task<float> Handle(
        PreviewEquipItemBasicAttackDamageQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        var items = await context
            .Items.AsNoTracking()
            .Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == query.CreatureId
            )
            .ToArrayAsync(cancellationToken);

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
            new GetAllWeaponProficienciesQuery
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
