using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.NpcConversations.Tools;

internal class StartConversationTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    IQueryHandler<
        GetNpcConversationBriefingQuery,
        NpcConversationBriefing
    > getNpcConversationBriefing,
    ICommandHandler<OpenNpcConversationCommand, OpenNpcConversationResult> openNpcConversation,
    ILogger<StartConversationTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("start_conversation")]
    [Description(
        "Call this when you begin talking to someone. It returns their player-visible appearance, private roleplaying background, reputation-driven attitude, conversation history, and quest context."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person you're speaking with, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[start_conversation] npcName={NpcName}", npcName);
        var stopwatch = Stopwatch.StartNew();

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return new ToolError(
                "An encounter is underway — resolve it before starting a conversation."
            );
        }

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

        if (npc == null)
        {
            return new ToolError(
                $"No one named '{npcName}' found nearby. Call look to see who's around."
            );
        }

        var outcome = await openNpcConversation.Handle(
            new OpenNpcConversationCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                NpcId = npc.Id,
                NpcName = npc.Name,
            },
            cancellationToken
        );
        if (outcome == OpenNpcConversationResult.AlreadyOpen)
        {
            return new ToolError(
                $"You are already in conversation with {npcName}; no need to call this again for them. If the dialogue has turned to someone else, call lookup instead."
            );
        }

        var result = await getNpcConversationBriefing.Handle(
            new GetNpcConversationBriefingQuery
            {
                NpcId = npc.Id,
                PlayerId = player.Id,
                WorldId = turnContext.WorldId,
                LocationId = player.LocationId,
            },
            cancellationToken
        );
        logger.LogInformation(
            "[perf] [start_conversation] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
        return result;
    }
}
