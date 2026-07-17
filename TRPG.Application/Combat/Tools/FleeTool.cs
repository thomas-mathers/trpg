using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;

namespace TRPG.Application.Combat.Tools;

internal class FleeTool(
    GameTurnContext turnContext,
    GetCombatantsQueryHandler getCombatants,
    EndCombatCommandHandler endCombat,
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

        var combatants = await getCombatants.Handle(
            new GetCombatantsQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        if (combatants is not { Count: > 0 })
        {
            return new ToolError("There's no fight to flee from right now.");
        }

        var state = combatEngine.ResolveFlee(combatants);

        await endCombat.Handle(
            new EndCombatCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                State = state,
            },
            cancellationToken
        );

        var result = state.ToCombatResult();

        logger.LogInformation(
            "[perf] [flee] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
