using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

internal class CombatantFactory(
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventory,
    IQueryHandler<
        GetAllWeaponProficienciesQuery,
        IReadOnlyDictionary<WeaponType, int>
    > getAllWeaponProficiencies,
    IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
    IOptionsSnapshot<CombatOptions> optionsSnapshot
)
{
    public async Task<Combatant> Create(
        Creature creature,
        bool isPlayer,
        CancellationToken cancellationToken = default
    )
    {
        var weaponProficiencies = await getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery
            {
                WorldId = creature.WorldId,
                CreatureId = creature.Id,
            },
            cancellationToken
        );

        var items = await getInventory.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(creature.Id, OwnerType.Creature),
            },
            cancellationToken
        );

        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = creature.Id },
            cancellationToken
        );

        return Combatant.FromCreature(
            optionsSnapshot.Value,
            isPlayer,
            creature,
            abilities,
            items,
            weaponProficiencies
        );
    }
}
