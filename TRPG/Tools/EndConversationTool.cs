using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal class EndConversationTool : Tool, IInvokableTool {
    private readonly ILogger<EndConversationTool> _logger;
    private readonly NpcConversationService _npcConversationService;
    private readonly GameSession _session;

    public EndConversationTool(GameSession session, NpcConversationService npcConversationService,
        ILogger<EndConversationTool> logger) {
        _session = session;
        _npcConversationService = npcConversationService;
        _logger = logger;

        Function = new Function {
            Name = "end_conversation",
            Description =
                "Call this when a conversation with someone winds down or the topic changes significantly, to save a summary of what was discussed so you can recall it next time you speak with them.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property> {
                    ["npcName"] = new() {
                        Type = "string",
                        Description =
                            "The exact Name of the person you spoke with, copied verbatim from the most recent look or move result."
                    },
                    ["summary"] = new() {
                        Type = "string",
                        Description =
                            "A concise, third-person, factual summary of what was discussed — replaces any previous summary for this person."
                    }
                },
                Required = new List<string> { "npcName", "summary" }
            }
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        if (args is null || !args.TryGetValue("npcName", out var npcNameRaw) ||
            string.IsNullOrWhiteSpace(npcNameRaw?.ToString())) {
            return new { Error = "No npcName provided." };
        }

        if (!args.TryGetValue("summary", out var summaryRaw) || string.IsNullOrWhiteSpace(summaryRaw?.ToString())) {
            return new { Error = "No summary provided." };
        }

        var npcName = npcNameRaw.ToString()!;
        var summary = summaryRaw.ToString()!;

        _logger.LogInformation("[end_conversation] npcName={NpcName}", npcName);
        var result = InvokeMethodAsync(npcName, summary, CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[end_conversation] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(string npcName, string summary, CancellationToken cancellationToken) {
        if (!_session.ActiveConversationNpcs.TryGetValue(npcName, out var npcId)) {
            return new { Error = $"No open conversation with '{npcName}'. Call start_conversation first." };
        }

        await _npcConversationService.SetSummary(_session.WorldId, _session.PlayerId, npcId, summary,
            cancellationToken);
        _session.ActiveConversationNpcs.Remove(npcName);

        return new { Saved = true };
    }
}