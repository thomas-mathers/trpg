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
    IQueryHandler<
        GetInventoryItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getInventoryByOwners,
    IQueryHandler<
        GetWeaponProficienciesByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<WeaponType, int>>
    > getWeaponProficienciesByCreatureIds,
    IQueryHandler<
        GetCreatureAbilitiesByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Ability>>
    > getCreatureAbilitiesByCreatureIds,
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

    public async Task<IReadOnlyList<Combatant>> CreateMany(
        Guid worldId,
        IReadOnlyCollection<Creature> creatures,
        Guid playerId,
        CancellationToken cancellationToken = default
    )
    {
        var creatureIds = creatures.Select(creature => creature.Id).ToArray();

        var itemsByCreature = await getInventoryByOwners.Handle(
            new GetInventoryItemsByOwnersQuery { CreatureIds = creatureIds },
            cancellationToken
        );

        var weaponProficienciesByCreature = await getWeaponProficienciesByCreatureIds.Handle(
            new GetWeaponProficienciesByCreatureIdsQuery
            {
                WorldId = worldId,
                CreatureIds = creatureIds,
            },
            cancellationToken
        );

        var abilitiesByCreature = await getCreatureAbilitiesByCreatureIds.Handle(
            new GetCreatureAbilitiesByCreatureIdsQuery { CreatureIds = creatureIds },
            cancellationToken
        );

        return creatures
            .Select(creature =>
                Combatant.FromCreature(
                    optionsSnapshot.Value,
                    creature.Id == playerId,
                    creature,
                    abilitiesByCreature[creature.Id],
                    itemsByCreature.GetValueOrDefault(creature.Id, []),
                    weaponProficienciesByCreature[creature.Id]
                )
            )
            .ToArray();
    }
}
