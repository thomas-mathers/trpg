using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.Quests.Commands;
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
        "Call this whenever the conversation turns to accepting an available job, or to finishing, reporting back on, or being paid for a ready-to-complete one — including the player merely asking about a reward or payment. It opens the quest details dialog, where accepting or completing actually happens. Accepting a quest, completing a quest, and paying a reward are NOT things you narrate in prose: they only happen through this tool, and if you narrate any of them without calling it, nothing is actually granted, yet your own narration becomes this NPC's permanent memory and will wrongly convince every future conversation the quest was already resolved. When in doubt whether the player means this, call it rather than narrate. Use only an exact NPC and quest Name returned by start_conversation; never call it merely because an NPC mentioned work in passing."
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
            JsonSerializer.Serialize(
                result,
                TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
        return result;
    }
}
