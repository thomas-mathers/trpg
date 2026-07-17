using Microsoft.EntityFrameworkCore;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Queries;

internal class GetCombatantsQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetCombatantsQueryHandler(
    TrpgDbContext context,
    GetCreatureByIdQueryHandler getCreatureById,
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
        var snapshots = await context
            .Combatants.AsNoTracking()
            .Where(c => c.SessionId == query.SessionId)
            .ToArrayAsync(cancellationToken);

        var combatants = new List<Combatant>();
        foreach (var snapshot in snapshots)
        {
            var creature = await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = snapshot.CreatureId },
                cancellationToken
            );
            var weaponProficiencies = (
                await getAllWeaponProficiencies.Handle(
                    new GetAllWeaponProficienciesQuery
                    {
                        WorldId = creature!.WorldId,
                        CreatureId = snapshot.CreatureId,
                    },
                    cancellationToken
                )
            ).ToDictionary(kv => kv.Key, kv => kv.Value);

            IReadOnlyList<Item> inventory;
            IReadOnlyList<Ability> abilities;
            if (snapshot.IsPlayer)
            {
                var inventoryItems = await getInventoryByCreatureId.Handle(
                    new GetInventoryByCreatureIdQuery { CreatureId = snapshot.CreatureId },
                    cancellationToken
                );
                inventory = inventoryItems
                    .Where(i => i.EquippedSlot != null)
                    .Select(i => i.Item)
                    .ToArray();

                var abilityNames = await getCreatureAbilities.Handle(
                    new GetCreatureAbilitiesQuery
                    {
                        WorldId = creature.WorldId,
                        CreatureId = snapshot.CreatureId,
                    },
                    cancellationToken
                );
                var ownedAbilities = abilityNames
                    .Select(abilityDefinitions.GetByName)
                    .OfType<Ability>()
                    .ToArray();
                abilities = new[] { abilityDefinitions.BasicAttack }
                    .Concat(ownedAbilities)
                    .ToArray();
            }
            else
            {
                inventory = [];
                abilities = [abilityDefinitions.BasicAttack];
            }

            combatants.Add(
                snapshot.ToCombatant(creature, abilities, inventory, weaponProficiencies)
            );
        }

        return combatants;
    }
}
