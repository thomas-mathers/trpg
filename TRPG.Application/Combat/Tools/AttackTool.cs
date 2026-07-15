using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Tools;
using TRPG.Application.Tools.Common;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Tools;

internal class AttackTool(
    GameSession session,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreaturesByIdsQueryHandler getCreaturesByIds,
    GetAllNearbyCreaturesQueryHandler getAllNearbyCreatures,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    AdjustWeaponProficienciesCommandHandler adjustWeaponProficiencies,
    ApplyCombatRewardsCommandHandler applyCombatRewards,
    UpdateCreaturesCommandHandler updateCreatureCommandHandler,
    AbilityDefinitions abilityDefinitions,
    CombatEngine combatEngine,
    ILogger<AttackTool> logger
) : IGameTool
{
    private static readonly IReadOnlyCollection<CreatureType> HostileCreatureTypes =
        Enum.GetValues<CreatureType>().Except(CreatureTypes.Humanoid).ToArray();

    public Delegate Invoke => InvokeAsync;

    [DisplayName("attack")]
    [Description(
        "Attacks a hostile creature by name, using the named ability. If not already in combat, this starts an encounter with every hostile creature nearby, not just the named target. Use one of the player's own learned abilities, or \"Strike\" for a plain unenhanced attack with whatever is equipped."
    )]
    private async Task<object?> InvokeAsync(
        [Description("The exact name of the ability to use.")] string abilityName,
        [Description(
            "The exact name of the creature to attack, copied verbatim from the most recent look result or combat result."
        )]
            string targetName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[attack] abilityName={AbilityName} targetName={TargetName}",
            abilityName,
            targetName
        );
        var stopwatch = Stopwatch.StartNew();

        if (session.Combatants is not { Count: > 0 })
        {
            await StartFight(targetName, cancellationToken);
        }

        var state = combatEngine.ProcessRound(session.Combatants!, abilityName, targetName);

        if (state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled)
        {
            await EndFight(state, cancellationToken);
        }

        var result = state.ToCombatResult();

        logger.LogInformation(
            "[perf] [attack] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }

    private async Task StartFight(string targetName, CancellationToken cancellationToken)
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
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

        if (nearby.All(c => c.Name != targetName))
        {
            throw new InvalidOperationException(
                $"No '{targetName}' found nearby to attack. Call look to see what's around."
            );
        }

        var playerCombatant = await BuildPlayerCombatant(player, cancellationToken);

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

        session.Combatants = new[] { playerCombatant }.Concat(enemyCombatants).ToArray();
    }

    private async Task EndFight(CombatState state, CancellationToken cancellationToken)
    {
        var playerId = state.Combatants.Single(c => c.IsPlayer).Id;

        await adjustWeaponProficiencies.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = session.WorldId,
                CreatureId = playerId,
                ProficiencyDeltas = state.WeaponSwingCounts,
            },
            cancellationToken
        );

        if (state.Outcome == CombatOutcome.Victory)
        {
            await applyCombatRewards.Handle(
                new ApplyCombatRewardsCommand
                {
                    CreatureId = playerId,
                    ExperienceGained = state.XpGained ?? 0,
                    GoldGained = state.GoldLooted ?? 0,
                },
                cancellationToken
            );

            var enemyCreatureIds = state
                .Combatants.Where(c => !c.IsPlayer)
                .Select(c => c.Id)
                .ToArray();

            await updateCreatureCommandHandler.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = enemyCreatureIds,
                    State = CreatureState.Dead,
                },
                cancellationToken
            );
        }

        session.Combatants = null;
    }

    private async Task<Combatant> BuildPlayerCombatant(
        Creature player,
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
            new GetAllWeaponProficienciesQuery
            {
                WorldId = session.WorldId,
                CreatureId = player.Id,
            },
            cancellationToken
        );

        var abilityNames = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { WorldId = session.WorldId, CreatureId = player.Id },
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
