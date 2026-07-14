using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Tools;

internal record CombatResult(
    CombatOutcome Outcome,
    PlayerCombatState Player,
    IReadOnlyList<EnemyCombatState> Enemies,
    IReadOnlyList<CombatEvent> Events,
    int? XpGained,
    int? GoldLooted
);

internal class AttackTool(
    GameSession session,
    GetCreatureByIdQueryHandler getCreatureById,
    GetAllNearbyCreaturesQueryHandler getAllNearbyCreatures,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    GetCreatureAbilitiesQueryHandler getCreatureAbilities,
    ApplyWeaponSwingGainsCommandHandler applyWeaponSwingGains,
    ApplyCombatRewardsCommandHandler applyCombatRewards,
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
            var (combatants, error) = await BuildCombatants(targetName, cancellationToken);
            if (error is not null)
            {
                return new { Error = error };
            }

            session.Combatants = combatants;
        }

        CombatState state;
        try
        {
            state = combatEngine.ProcessRound(session.Combatants!, abilityName, targetName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return new { Error = ex.Message };
        }

        if (state.Outcome != CombatOutcome.Ongoing)
        {
            var playerId = session.Combatants!.Single(c => c.IsPlayer).CreatureId;
            await applyWeaponSwingGains.Handle(
                new ApplyWeaponSwingGainsCommand
                {
                    WorldId = session.WorldId,
                    CreatureId = playerId,
                    SwingCounts = state.WeaponSwingCounts,
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
            }

            session.Combatants = null;
        }

        var result = new CombatResult(
            state.Outcome,
            state.Player,
            state.Enemies,
            state.Events,
            state.XpGained,
            state.GoldLooted
        );

        logger.LogInformation(
            "[perf] [attack] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }

    private async Task<CombatantBuildResult> BuildCombatants(
        string targetName,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        var nearby = await getAllNearbyCreatures.Handle(
            new GetAllNearbyCreaturesQuery
            {
                Location = new CreatureLocation(
                    session.WorldId,
                    player!.RoomId,
                    player.StateId,
                    player.DistrictId
                ),
                ExcludingCreatureId = player.Id,
                CreatureTypes = HostileCreatureTypes,
            },
            cancellationToken
        );

        if (nearby.Count == 0)
        {
            return new CombatantBuildResult(null, "There's nothing here to attack.");
        }

        if (nearby.All(c => c.Name != targetName))
        {
            return new CombatantBuildResult(
                null,
                $"No '{targetName}' found nearby to attack. Call look to see what's around."
            );
        }

        var playerCombatant = await BuildPlayerCombatant(player, cancellationToken);

        var enemyCombatants = new List<Combatant>();
        foreach (var summary in nearby)
        {
            var enemyCreature = await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = summary.Id },
                cancellationToken
            );
            enemyCombatants.Add(
                Combatant.FromCreature(
                    enemyCreature!,
                    [],
                    abilityDefinitions.BasicAttack,
                    isPlayer: false,
                    [],
                    []
                )
            );
        }

        IReadOnlyList<Combatant> combatants = [playerCombatant, .. enemyCombatants];
        return new CombatantBuildResult(combatants, null);
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
            .Select(name => abilityDefinitions.GetByName(name))
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

    private record CombatantBuildResult(IReadOnlyList<Combatant>? Combatants, string? Error);
}
