using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.Quests.Tools;

internal class ShowQuestDetailsTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    ICommandHandler<RequestQuestDialogCommand, QuestDialogRequestResult> requestQuestDialog,
    ILogger<ShowQuestDetailsTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("show_quest_details")]
    [Description(
        "Call this after the player explicitly says they want to accept an available job or turn in completed work. It opens the quest details dialog. Use only an exact NPC and quest Name returned by start_conversation; never call it merely because an NPC mentioned work."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the NPC currently being spoken with, copied verbatim from start_conversation."
        )]
            string npcName,
        [Description(
            "The exact Name of an available or ready-to-complete quest returned by start_conversation."
        )]
            string questName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[show_quest_details] npcName={NpcName} questName={QuestName}",
            npcName,
            questName
        );
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );
        var npc = await getCreatureByNameAtLocation.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = turnContext.WorldId,
                LocationId = player!.LocationId,
                Name = npcName,
            },
            cancellationToken
        );

        if (npc is null)
        {
            return new ToolError($"No one named '{npcName}' found nearby.");
        }

        var dialog = await requestQuestDialog.Handle(
            new RequestQuestDialogCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = player.Id,
                GiverId = npc.Id,
                QuestName = questName,
            },
            cancellationToken
        );
        if (!dialog.IsAvailable)
        {
            return new ToolError($"'{questName}' is not available from {npcName} right now.");
        }

        var result = new
        {
            Presented = true,
            Instruction = "The quest details dialog is open. Stop the response without narration.",
        };
        logger.LogInformation(
            "[perf] [show_quest_details] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, TRPG.Contracts.TrpgJsonOptions.Default)
        );
        return result;
    }
}
