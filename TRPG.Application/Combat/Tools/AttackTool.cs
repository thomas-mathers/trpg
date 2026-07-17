using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.Common.Tools;

namespace TRPG.Application.Combat.Tools;

internal class AttackTool(
    GameTurnContext turnContext,
    GetCombatantsQueryHandler getCombatants,
    SetCombatantsCommandHandler setCombatants,
    StartCombatCommandHandler startCombat,
    EndCombatCommandHandler endCombat,
    CombatEngine combatEngine,
    ILogger<AttackTool> logger
) : IGameTool
{
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

        var combatants = await getCombatants.Handle(
            new GetCombatantsQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        if (combatants is not { Count: > 0 })
        {
            combatants = await startCombat.Handle(
                new StartCombatCommand
                {
                    SessionId = turnContext.SessionId,
                    WorldId = turnContext.WorldId,
                    PlayerId = turnContext.PlayerId,
                    TargetName = targetName,
                },
                cancellationToken
            );
        }

        var state = combatEngine.ProcessRound(combatants!, abilityName, targetName);

        if (state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled)
        {
            await endCombat.Handle(
                new EndCombatCommand
                {
                    SessionId = turnContext.SessionId,
                    WorldId = turnContext.WorldId,
                    State = state,
                },
                cancellationToken
            );
        }
        else
        {
            await setCombatants.Handle(
                new SetCombatantsCommand
                {
                    SessionId = turnContext.SessionId,
                    Combatants = combatants!,
                },
                cancellationToken
            );
        }

        var result = state.ToCombatResult();

        logger.LogInformation(
            "[perf] [attack] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
