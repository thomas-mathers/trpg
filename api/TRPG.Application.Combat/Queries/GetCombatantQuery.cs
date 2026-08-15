using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Handling;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Queries;

public class GetCombatantQuery
{
    public required Creature Creature { get; init; }
    public required bool IsPlayer { get; init; }
}

public class GetCombatantQueryHandler(
    IQueryHandler<GetInventoryByOwnerQuery, IReadOnlyList<Item>> getInventory,
    IQueryHandler<
        GetAllWeaponProficienciesQuery,
        IReadOnlyDictionary<WeaponType, int>
    > getAllWeaponProficiencies,
    IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
    IOptionsSnapshot<CombatOptions> optionsSnapshot
) : IQueryHandler<GetCombatantQuery, Combatant>
{
    public async Task<Combatant> Handle(
        GetCombatantQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var weaponProficiencies = await getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery
            {
                WorldId = query.Creature.WorldId,
                CreatureId = query.Creature.Id,
            },
            cancellationToken
        );

        var items = await getInventory.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(query.Creature.Id, OwnerType.Creature),
            },
            cancellationToken
        );

        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = query.Creature.Id },
            cancellationToken
        );

        return Combatant.FromCreature(
            optionsSnapshot.Value,
            query.IsPlayer,
            query.Creature,
            abilities,
            items,
            weaponProficiencies
        );
    }
}
