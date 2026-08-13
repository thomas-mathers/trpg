using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;

namespace TRPG.Application.Combat.Tools;

internal class StartFightTool(
    GameTurnContext turnContext,
    IGameClientEventSink gameEvents,
    GetActiveFightQueryHandler getActiveFight,
    StartFightCommandHandler startFight,
    ILogger<StartFightTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("attack")]
    [Description(
        "Starts combat with a hostile creature by name. If no fight is active, every hostile creature nearby joins the encounter. Combat begins without resolving an attack; the player chooses the first and all later actions from the combat menu."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact name of the creature to attack, copied verbatim from the most recent look result or combat result."
        )]
            string targetName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[attack] targetName={TargetName}", targetName);
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

        gameEvents.Enqueue(new CombatStartedEvent(FightStateMapper.ToFightState(combatants)));

        logger.LogInformation(
            "[perf] [attack] combat started in {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );
        return new
        {
            Message = "Combat has started. The player will choose the first action from the combat menu.",
        };
    }
}
