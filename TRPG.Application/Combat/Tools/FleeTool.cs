using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;
using TRPG.Application.WeaponProficiency.Commands;

namespace TRPG.Application.Combat.Tools;

internal class FleeTool(
    GameTurnContext turnContext,
    GetCombatantsQueryHandler getCombatants,
    PersistCombatantsCommandHandler persistCombatants,
    EndFightCommandHandler endFight,
    AdjustWeaponProficienciesCommandHandler adjustWeaponProficiencies,
    CombatEngine combatEngine,
    ICombatStatusPublisher combatStatusPublisher,
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
            new GetCombatantsQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
            },
            cancellationToken
        );
        if (combatants is not { Count: > 0 })
        {
            return new ToolError("There's no fight to flee from right now.");
        }

        var state = combatEngine.ResolveFlee(combatants);

        await persistCombatants.Handle(
            new PersistCombatantsCommand { Combatants = combatants },
            cancellationToken
        );

        if (state.WeaponSwingCounts.Count > 0)
        {
            await adjustWeaponProficiencies.Handle(
                new AdjustWeaponProficienciesCommand
                {
                    WorldId = turnContext.WorldId,
                    CreatureId = turnContext.PlayerId,
                    ProficiencyDeltas = state.WeaponSwingCounts,
                },
                cancellationToken
            );
        }

        await endFight.Handle(
            new EndFightCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                State = state,
            },
            cancellationToken
        );

        await combatStatusPublisher.PublishCombatStatus(turnContext.WorldId, [], cancellationToken);

        var result = state.ToCombatResult();

        logger.LogInformation(
            "[perf] [flee] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
