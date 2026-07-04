using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal record StartConversationResult(string Summary);

internal class StartConversationTool : Tool, IInvokableTool {
    private readonly ILogger<StartConversationTool> _logger;
    private readonly NpcConversationService _npcConversationService;
    private readonly PersonService _personService;
    private readonly GameSession _session;

    public StartConversationTool(GameSession session, PersonService personService,
        NpcConversationService npcConversationService, ILogger<StartConversationTool> logger) {
        _session = session;
        _personService = personService;
        _npcConversationService = npcConversationService;
        _logger = logger;

        Function = new Function {
            Name = "start_conversation",
            Description =
                "Call this when you begin talking to someone, to remember what was discussed the last time you spoke with them. Returns an empty summary if you've never spoken before.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property> {
                    ["npcName"] = new() {
                        Type = "string",
                        Description =
                            "The exact Name of the person you're speaking with, copied verbatim from the most recent look or move result."
                    }
                },
                Required = new List<string> { "npcName" }
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        if (args is null || !args.TryGetValue("npcName", out var npcNameRaw) ||
            string.IsNullOrWhiteSpace(npcNameRaw?.ToString())) {
            return new { Error = "No npcName provided." };
        }

        var npcName = npcNameRaw.ToString()!;

        _logger.LogDebug("[start_conversation] npcName={NpcName}", npcName);
        var result = InvokeMethodAsync(npcName, CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogDebug("[start_conversation] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(string npcName, CancellationToken cancellationToken) {
        var player = await _personService.GetById(_session.PlayerId, cancellationToken);
        var npc = await _personService.GetByNameNearby(_session.WorldId, player!, npcName, cancellationToken);

        if (npc == null) {
            return new { Error = $"No one named '{npcName}' found nearby. Call look to see who's around." };
        }

        var summary = await _npcConversationService.GetSummary(player!.Id, npc.Id, cancellationToken);

        _session.ActiveConversationNpcs[npc.Name] = npc.Id;

        return new StartConversationResult(summary);
    }
}