using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;
using TRPG.Contracts.Combat.Requests;

namespace TRPG.Application.Combat.Tools;

internal class StartFightTool(
    GameTurnContext turnContext,
    GetActiveFightQueryHandler getActiveFight,
    StartFightCommandHandler startFight,
    CombatEngine combatEngine,
    ResolveCombatRoundCommandHandler resolveCombatRound,
    ILogger<StartFightTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("attack")]
    [Description(
        "Attacks a hostile creature by name, using the named ability. If not already in combat, this starts an encounter with every hostile creature nearby, not just the named target, and resolves the first round. Use one of the player's own learned abilities, or \"Strike\" for a plain unenhanced attack with whatever is equipped. Once a fight is underway, all further rounds are resolved through the player's combat menu, not through this tool."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact name of the creature to attack, copied verbatim from the most recent look result or combat result."
        )]
            string targetName,
        [Description("The exact name of the ability to use.")] string abilityName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[attack] abilityName={AbilityName} targetName={TargetName}",
            abilityName,
            targetName
        );
        var stopwatch = Stopwatch.StartNew();

        var activeFight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeFight != null)
        {
            return new ToolError(
                "A fight is already underway — resolve it through the player's combat menu, not this tool."
            );
        }

        var combatants = await startFight.Handle(
            new StartFightCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                TargetName = targetName,
            },
            cancellationToken
        );

        var targetId = combatants.First(c => c.Name == targetName).CreatureId;

        var resolverResult = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(targetId, abilityName)
        );

        if (resolverResult.ErrorMessage is not null)
        {
            return new ToolError(resolverResult.ErrorMessage!);
        }

        var state = combatEngine.ProcessRound(combatants, resolverResult.Result!);

        turnContext.PendingEvents.Add(new CombatStartedEvent(combatants));

        var result = await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                Combatants = combatants,
                State = state,
            },
            cancellationToken
        );

        logger.LogInformation(
            "[perf] [attack] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
