using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

internal class StartFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string TargetName { get; init; }
}

internal class StartFightCommandHandler(
    TrpgDbContext context,
    GetCreatureByIdQueryHandler getCreatureById,
    GetAllNearbyCreaturesQueryHandler getAllNearbyCreatures,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    ApplyPassiveRegenCommandHandler applyPassiveRegen,
    AbilityDefinitions abilityDefinitions
)
{
    private static readonly IReadOnlyCollection<CreatureType> HostileCreatureTypes =
        Enum.GetValues<CreatureType>().Except(CreatureTypes.Humanoid).ToArray();

    public async Task<IReadOnlyList<Combatant>> Handle(
        StartFightCommand command,
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

        var enemyIds = nearby.Select(summary => summary.Id).ToArray();

        var regeneratedCreatures = await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = command.SessionId,
                CreatureIds = [player.Id, .. enemyIds],
            },
            cancellationToken
        );

        var playerCombatant = await BuildPlayerCombatant(
            regeneratedCreatures[player.Id],
            command.WorldId,
            cancellationToken
        );

        var enemyCombatants = enemyIds
            .Select(enemyId =>
                Combatant.FromCreature(
                    regeneratedCreatures[enemyId],
                    [],
                    abilityDefinitions.BasicAttack,
                    abilityDefinitions.BlockStance,
                    isPlayer: false,
                    [],
                    new Dictionary<WeaponType, int>(),
                    []
                )
            )
            .ToList();

        var combatants = new[] { playerCombatant }.Concat(enemyCombatants).ToArray();

        context.Fights.Add(
            new Fight
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                CombatantIds = combatants.Select(c => c.CreatureId).ToList(),
                StartedAt = DateTime.UtcNow,
            }
        );
        await context.SaveChangesAsync(cancellationToken);

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
        var usableItems = UsableItem.FromInventory(inventoryItems);

        var weaponProficiencies = await getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = worldId, CreatureId = player.Id },
            cancellationToken
        );

        var abilityNames = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = player.Id },
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
            abilityDefinitions.BlockStance,
            isPlayer: true,
            equipped,
            weaponProficiencies,
            usableItems
        );
    }
}
