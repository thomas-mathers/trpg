using System.ComponentModel;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.Props.Queries;
using TRPG.Domain.Models;
using TRPG.GameTurns.Mappers;
using TRPG.Tools;

namespace TRPG.GameTurns.Tools;

internal record UnlockCellToolResult(bool Opened, MoveToolHostileEncounter? HostileEncounter);

internal class UnlockCellTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCellByNameAtLocationQuery, Cell?> getCellByNameAtLocation,
    ICommandHandler<AttemptCellUnlockCommand, AttemptCellUnlockResult> attemptCellUnlock,
    ILogger<UnlockCellTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("unlock_cell")]
    [Description(
        "Attempts to unlock a nearby locked cell by exact name, either by picking its lock or by using a matching key the player is carrying. Call this ONLY after the player explicitly says they want to unlock, pick, or open the cell — never automatically. If a hostile guard is nearby and the attempt is not a clean key-unlock, there is a chance the guard notices and a fight starts; narrate that outcome if it happens. Opened false means only that this attempt failed — the player may try again. Never invent anyone else arriving to interrupt; only creatures the scene actually lists are present."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a nearby cell, copied verbatim from the most recent look result."
        )]
            string cellName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[unlock_cell] cellName={CellName}", cellName);

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            logger.LogInformation("[unlock_cell] refused: an encounter is already active");
            return new ToolError("You can't do that while an encounter is active.");
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        var cell = await getCellByNameAtLocation.Handle(
            new GetCellByNameAtLocationQuery
            {
                WorldId = turnContext.WorldId,
                LocationId = player!.LocationId,
                Name = cellName,
            },
            cancellationToken
        );
        if (cell == null)
        {
            logger.LogInformation("[unlock_cell] refused: no cell named {CellName} here", cellName);
            return new ToolError($"There's no cell called '{cellName}' here.");
        }

        var result = await attemptCellUnlock.Handle(
            new AttemptCellUnlockCommand
            {
                PlayerId = turnContext.PlayerId,
                WorldId = turnContext.WorldId,
                CellId = cell.Id,
            },
            cancellationToken
        );

        if (result.Outcome == CellUnlockOutcome.NothingToUnlock)
        {
            logger.LogInformation("[unlock_cell] refused: {CellName} isn't locked", cellName);
            return new ToolError($"'{cellName}' isn't locked.");
        }

        logger.LogInformation(
            "[unlock_cell] outcome={Outcome}, encounter={Encounter}",
            result.Outcome,
            result.Encounter?.GetType().Name ?? "none"
        );

        return new UnlockCellToolResult(
            result.Outcome == CellUnlockOutcome.Opened,
            result.Encounter?.ToMoveToolSummary()
        );
    }
}
