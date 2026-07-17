using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Tools;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;

namespace TRPG.Application.Tools;

internal class LookupTool(
    GameTurnContext turnContext,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreatureByNameNearbyQueryHandler getCreatureByNameNearby,
    GetCreatureKnowledgeQueryHandler getCreatureKnowledge,
    GetPlaytimeQueryHandler getPlaytime,
    ILogger<LookupTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("lookup")]
    [Description(
        "Returns background information about a named country, city, faction, or person anywhere in the world — automatically figures out which kind of thing the name refers to, so you don't need to know its category in advance. Partial names and misspellings resolve automatically: results come back as a list ranked by Similarity (1.0 = exact), with full details for the best match and name-only stubs for the others — to expand a stub, call lookup again with its exact Name. This represents what a specific NPC knows or has heard, not omniscient narrator knowledge — call it on behalf of the NPC who would be recalling or sharing this information in dialogue, never to generate the narrator's own scene-setting exposition. Call this whenever dialogue turns to a specific person, place, or faction mentioned by name — a family member, acquaintance, distant place, or faction — other than the NPC you're currently speaking with about themselves: their family, hometown, faction ties, workplace (and whether they own it), work hours, days off, and home are already given in full by start_conversation's biography, and if it doesn't mention something, that means it doesn't exist, so don't call this tool on yourself to check. Returns an Error if nothing matches the name, or if the speaker wouldn't know about it."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the NPC whose knowledge this represents — the character who would be recalling or sharing this information, copied verbatim from the most recent look or move result."
        )]
            string askingPersonName,
        [Description(
            "The name of the country, city, faction, or person to look up — a first name or close spelling is fine; the closest matches are returned."
        )]
            string subjectName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[lookup] askingPersonName={AskingPersonName} subjectName={SubjectName}",
            askingPersonName,
            subjectName
        );
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );
        var askingPerson = await getCreatureByNameNearby.Handle(
            new GetCreatureByNameNearbyQuery
            {
                WorldId = turnContext.WorldId,
                Player = player!,
                Name = askingPersonName,
            },
            cancellationToken
        );
        if (askingPerson == null)
        {
            return new ToolError(
                $"No one named '{askingPersonName}' found nearby. Call look to see who's around."
            );
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        var currentYear = GameClock.GetCurrentInGameDate(playtime).Year;
        var query = new GetCreatureKnowledgeQuery
        {
            WorldId = turnContext.WorldId,
            SubjectName = subjectName,
            CurrentYear = currentYear,
            AskingPerson = askingPerson,
        };
        var matches = await getCreatureKnowledge.Handle(query, cancellationToken);

        object result =
            matches.Count > 0
                ? matches
                : new ToolError(
                    $"'{subjectName}' isn't recognized as a country, city, faction, or person that {askingPersonName} would know about."
                );

        logger.LogInformation(
            "[perf] [lookup] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
