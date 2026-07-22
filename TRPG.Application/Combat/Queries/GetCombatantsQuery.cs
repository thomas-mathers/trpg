using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Queries;

internal class GetCombatantsQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetCombatantsQueryHandler(
    GetActiveFightQueryHandler getActiveFight,
    GetCreaturesByIdsQueryHandler getCreaturesByIds,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    AbilityDefinitions abilityDefinitions
)
{
    public async Task<IReadOnlyList<Combatant>> Handle(
        GetCombatantsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = query.PlayerId },
            cancellationToken
        );

        if (fight == null)
        {
            return [];
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = fight.CombatantIds },
            cancellationToken
        );

        var combatants = new List<Combatant>();

        foreach (var creature in fight.CombatantIds.Select(id => creaturesById[id]))
        {
            var isPlayer = creature.Id == query.PlayerId;
            var weaponProficiencies = await getAllWeaponProficiencies.Handle(
                new GetAllWeaponProficienciesQuery
                {
                    WorldId = fight.WorldId,
                    CreatureId = creature.Id,
                },
                cancellationToken
            );

            IReadOnlyList<Item> inventory;
            IReadOnlyList<Ability> abilities;
            IReadOnlyList<UsableItem> usableItems;
            if (isPlayer)
            {
                var inventoryItems = await getInventoryByCreatureId.Handle(
                    new GetInventoryByCreatureIdQuery { CreatureId = creature.Id },
                    cancellationToken
                );
                inventory = inventoryItems
                    .Where(i => i.EquippedSlot != null)
                    .Select(i => i.Item)
                    .ToArray();
                usableItems = UsableItem.FromInventory(inventoryItems);

                var abilityNames = await getCreatureAbilities.Handle(
                    new GetCreatureAbilitiesQuery { CreatureId = creature.Id },
                    cancellationToken
                );
                abilities = abilityNames
                    .Select(abilityDefinitions.GetByName)
                    .OfType<Ability>()
                    .ToArray();
            }
            else
            {
                inventory = [];
                abilities = [];
                usableItems = [];
            }

            combatants.Add(
                Combatant.FromCreature(
                    creature,
                    abilities,
                    abilityDefinitions.BasicAttack,
                    abilityDefinitions.BlockStance,
                    isPlayer,
                    inventory,
                    weaponProficiencies,
                    usableItems
                )
            );
        }

        return combatants;
    }
}
