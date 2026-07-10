using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Application.Conversations.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;

namespace TRPG.Application.Tools;

internal record StartConversationResult(string Summary, string Biography);

internal class StartConversationTool : Tool, IInvokableTool
{
    private readonly GetCreatureByIdQueryHandler _getCreatureById;
    private readonly GetCreatureByNameNearbyQueryHandler _getCreatureByNameNearby;
    private readonly ILogger<StartConversationTool> _logger;
    private readonly GetConversationSummaryQueryHandler _getConversationSummary;
    private readonly GameSession _session;

    public StartConversationTool(
        GameSession session,
        GetCreatureByIdQueryHandler getCreatureById,
        GetCreatureByNameNearbyQueryHandler getCreatureByNameNearby,
        GetConversationSummaryQueryHandler getConversationSummary,
        ILogger<StartConversationTool> logger
    )
    {
        _session = session;
        _getCreatureById = getCreatureById;
        _getCreatureByNameNearby = getCreatureByNameNearby;
        _getConversationSummary = getConversationSummary;
        _logger = logger;

        Function = new Function
        {
            Name = "start_conversation",
            Description =
                "Call this when you begin talking to someone, to remember what was discussed the last time you spoke with them and to learn their personality, background, and manner of speech. Returns an empty summary if you've never spoken before — use the biography to voice them consistently regardless.",
            Parameters = new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>
                {
                    ["npcName"] = new()
                    {
                        Type = "string",
                        Description =
                            "The exact Name of the person you're speaking with, copied verbatim from the most recent look or move result.",
                    },
                },
                Required = new List<string> { "npcName" },
            },
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (
            args is null
            || !args.TryGetValue("npcName", out var npcNameRaw)
            || string.IsNullOrWhiteSpace(npcNameRaw?.ToString())
        )
        {
            return new { Error = "No npcName provided." };
        }

        var npcName = npcNameRaw.ToString()!;

        _logger.LogInformation("[start_conversation] npcName={NpcName}", npcName);
        var stopwatch = Stopwatch.StartNew();
        var result = InvokeMethodAsync(npcName, CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation(
            "[perf] [start_conversation] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            json
        );
        return json;
    }

    private async Task<object?> InvokeMethodAsync(
        string npcName,
        CancellationToken cancellationToken
    )
    {
        var player = await _getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = _session.PlayerId },
            cancellationToken
        );
        var npc = await _getCreatureByNameNearby.Handle(
            new GetCreatureByNameNearbyQuery
            {
                WorldId = _session.WorldId,
                Player = player!,
                Name = npcName,
            },
            cancellationToken
        );

        if (npc == null)
        {
            return new
            {
                Error = $"No one named '{npcName}' found nearby. Call look to see who's around.",
            };
        }

        if (_session.OpenConversationCreatureIdsByName.ContainsKey(npc.Name))
        {
            return new
            {
                Error = $"You are already in conversation with {npcName}; no need to call this again for them. If the dialogue has turned to someone else, call lookup instead.",
            };
        }

        var summary = await _getConversationSummary.Handle(
            new GetConversationSummaryQuery { CreatureId = player!.Id, NpcId = npc.Id },
            cancellationToken
        );

        _session.OpenConversationCreatureIdsByName[npc.Name] = npc.Id;

        return new StartConversationResult(summary, npc.Biography);
    }
}
