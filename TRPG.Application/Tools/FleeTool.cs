using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat;
using TRPG.Application.Game;
using TRPG.Application.WeaponProficiency.Commands;

namespace TRPG.Application.Tools;

internal class FleeTool(
    GameSession session,
    ApplyWeaponSwingGainsCommandHandler applyWeaponSwingGains,
    CombatEngine combatEngine,
    ILogger<FleeTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("flee")]
    [Description(
        "Attempts to flee the current combat encounter. Every enemy still standing gets a free attack against the player before the encounter ends. Only usable while in combat."
    )]
    private async Task<object?> InvokeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[flee] tool invoked");
        var stopwatch = Stopwatch.StartNew();

        if (session.Combatants is not { Count: > 0 })
        {
            return new { Error = "There's no fight to flee from right now." };
        }

        var state = combatEngine.ResolveFlee(session.Combatants);

        var playerId = session.Combatants.Single(c => c.IsPlayer).CreatureId;
        await applyWeaponSwingGains.Handle(
            new ApplyWeaponSwingGainsCommand
            {
                WorldId = session.WorldId,
                CreatureId = playerId,
                SwingCounts = state.WeaponSwingCounts,
            },
            cancellationToken
        );
        session.Combatants = null;

        var result = new CombatResult(
            state.Outcome,
            state.Player,
            state.Enemies,
            state.Events,
            state.XpGained,
            state.GoldLooted
        );

        logger.LogInformation(
            "[perf] [flee] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
