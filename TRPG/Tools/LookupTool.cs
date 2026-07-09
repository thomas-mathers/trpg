using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Services;

namespace TRPG.Tools;

internal class LookupTool : Tool, IInvokableTool
{
    private readonly CreatureKnowledgeService _creatureKnowledgeService;
    private readonly CreatureService _creatureService;
    private readonly ILogger<LookupTool> _logger;
    private readonly GameSession _session;

    public LookupTool(
        GameSession session,
        CreatureService creatureService,
        CreatureKnowledgeService creatureKnowledgeService,
        ILogger<LookupTool> logger
    )
    {
        _session = session;
        _creatureService = creatureService;
        _creatureKnowledgeService = creatureKnowledgeService;
        _logger = logger;

        Function = new Function
        {
            Name = "lookup",
            Description =
                "Returns background information about a named country, city, faction, or person anywhere in the world — automatically figures out which kind of thing the name refers to, so you don't need to know its category in advance. This represents what a specific NPC knows or has heard, not omniscient narrator knowledge — call it on behalf of the NPC who would be recalling or sharing this information in dialogue, never to generate the narrator's own scene-setting exposition. Call this whenever dialogue turns to a specific person, place, or faction mentioned by name — a family member, acquaintance, distant place, or faction — other than the NPC you're currently speaking with about themselves: their family, hometown, faction ties, workplace (and whether they own it), work hours, days off, and home are already given in full by start_conversation's biography, and if it doesn't mention something, that means it doesn't exist, so don't call this tool on yourself to check. Returns an Error if the name isn't recognized as any of these, or if the speaker wouldn't know about it.",
            Parameters = new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>
                {
                    ["askingPersonName"] = new()
                    {
                        Type = "string",
                        Description =
                            "The exact Name of the NPC whose knowledge this represents — the character who would be recalling or sharing this information, copied verbatim from the most recent look or move result.",
                    },
                    ["subjectName"] = new()
                    {
                        Type = "string",
                        Description =
                            "The exact Name of the country, city, faction, or person to look up, as it would appear in any tool result.",
                    },
                },
                Required = new List<string> { "askingPersonName", "subjectName" },
            },
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        if (
            args is null
            || !args.TryGetValue("askingPersonName", out var askingPersonNameRaw)
            || string.IsNullOrWhiteSpace(askingPersonNameRaw?.ToString())
        )
        {
            return new { Error = "No askingPersonName provided." };
        }

        if (
            !args.TryGetValue("subjectName", out var subjectNameRaw)
            || string.IsNullOrWhiteSpace(subjectNameRaw?.ToString())
        )
        {
            return new { Error = "No subjectName provided." };
        }

        var askingPersonName = askingPersonNameRaw.ToString()!;
        var subjectName = subjectNameRaw.ToString()!;

        _logger.LogInformation(
            "[lookup] askingPersonName={AskingPersonName} subjectName={SubjectName}",
            askingPersonName,
            subjectName
        );
        var result = InvokeMethodAsync(askingPersonName, subjectName, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[lookup] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(
        string askingPersonName,
        string subjectName,
        CancellationToken cancellationToken
    )
    {
        var player = await _creatureService.GetById(_session.PlayerId, cancellationToken);
        var askingPerson = await _creatureService.GetByNameNearby(
            _session.WorldId,
            player!,
            askingPersonName,
            cancellationToken
        );
        if (askingPerson == null)
        {
            return new
            {
                Error = $"No one named '{askingPersonName}' found nearby. Call look to see who's around.",
            };
        }

        var currentYear = GameClock.GetCurrentInGameDate(_session).Year;
        var query = new KnowledgeQuery(_session.WorldId, subjectName, currentYear, askingPerson);
        var result = await _creatureKnowledgeService.GetInfo(query, cancellationToken);

        return (object?)result
            ?? new
            {
                Error = $"'{subjectName}' isn't recognized as a country, city, faction, or person that {askingPersonName} would know about.",
            };
    }
}
