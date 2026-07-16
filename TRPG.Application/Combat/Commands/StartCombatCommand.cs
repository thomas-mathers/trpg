using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

internal class StartCombatCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string TargetName { get; init; }
}

internal class StartCombatCommandHandler(
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreaturesByIdsQueryHandler getCreaturesByIds,
    GetAllNearbyCreaturesQueryHandler getAllNearbyCreatures,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    SetCombatantsCommandHandler setCombatants,
    AbilityDefinitions abilityDefinitions
)
{
    private static readonly IReadOnlyCollection<CreatureType> HostileCreatureTypes =
        Enum.GetValues<CreatureType>().Except(CreatureTypes.Humanoid).ToArray();

    public async Task<IReadOnlyList<Combatant>> Handle(
        StartCombatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var nearby = await getAllNearbyCreatures.Handle(
            new GetAllNearbyCreaturesQuery
            {
                Location = new CreatureLocation(
                    player!.WorldId,
                    player.RoomId,
                    player.StateId,
                    player.DistrictId
                ),
                ExcludingCreatureId = player.Id,
                CreatureTypes = HostileCreatureTypes,
                IncludeDead = false,
            },
            cancellationToken
        );

        if (nearby.Count == 0)
        {
            throw new InvalidOperationException("There's nothing here to attack.");
        }

        if (nearby.All(c => c.Name != command.TargetName))
        {
            throw new InvalidOperationException(
                $"No '{command.TargetName}' found nearby to attack. Call look to see what's around."
            );
        }

        var playerCombatant = await BuildPlayerCombatant(player, command.WorldId, cancellationToken);

        var enemyCreatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = nearby.Select(summary => summary.Id).ToArray() },
            cancellationToken
        );
        var enemyCombatants = enemyCreatures
            .Select(enemyCreature =>
                Combatant.FromCreature(
                    enemyCreature,
                    [],
                    abilityDefinitions.BasicAttack,
                    isPlayer: false,
                    [],
                    []
                )
            )
            .ToList();

        var combatants = new[] { playerCombatant }.Concat(enemyCombatants).ToArray();

        await setCombatants.Handle(
            new SetCombatantsCommand { SessionId = command.SessionId, Combatants = combatants },
            cancellationToken
        );

        return combatants;
    }

    private async Task<Combatant> BuildPlayerCombatant(
        Creature player,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var inventoryItems = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = player.Id },
            cancellationToken
        );
        var equipped = inventoryItems
            .Where(i => i.EquippedSlot != null)
            .Select(i => i.Item)
            .ToArray();

        var weaponProficiencies = await getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = worldId, CreatureId = player.Id },
            cancellationToken
        );

        var abilityNames = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { WorldId = worldId, CreatureId = player.Id },
            cancellationToken
        );
        var abilities = abilityNames
            .Select(abilityDefinitions.GetByName)
            .OfType<Ability>()
            .ToArray();

        return Combatant.FromCreature(
            player,
            abilities,
            abilityDefinitions.BasicAttack,
            isPlayer: true,
            equipped,
            weaponProficiencies.ToDictionary()
        );
    }
}
