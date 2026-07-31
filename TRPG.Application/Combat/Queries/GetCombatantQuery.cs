using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Queries;

internal class GetCombatantQuery
{
    public required Creature Creature { get; init; }
    public required bool IsPlayer { get; init; }
}

internal class GetCombatantQueryHandler(
    GetInventoryByCreatureIdQueryHandler getInventory,
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    IOptionsSnapshot<CombatOptions> optionsSnapshot
)
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
            new GetInventoryByCreatureIdQuery { CreatureId = query.Creature.Id },
            cancellationToken
        );

        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = query.Creature.Id },
            cancellationToken
        );

        return Combatant.FromCreature(
            query.Creature,
            abilities,
            query.IsPlayer,
            items,
            weaponProficiencies,
            optionsSnapshot.Value
        );
    }
}
