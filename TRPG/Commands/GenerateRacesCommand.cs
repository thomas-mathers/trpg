using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateRacesCommand {
    public int Count { get; init; } = 6;
    public required string Description { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GenerateRacesCommandHandler(OllamaApiClient client, ILogger<GenerateRacesCommandHandler> logger) {
    public async Task<IReadOnlyList<Race>> Handle(
        GenerateRacesCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var schema = await client.GetJson<RaceListSchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             Generate {command.Count} unique races. Respond with a JSON object with a Races array, each element having Name, Description, and CultureStyle.
             Each description must be a single sentence. CultureStyle must be one of: Nordic/Viking, Roman, Feudal Japanese, Arabic/Persian, Celtic, Slavic, Ancient Egyptian, Mesoamerican, Byzantine, Mongol. Each race must have a different CultureStyle. You MUST respond in English only. Do not use markdown.
             """,
            $"Generate {command.Count} unique races.",
            s => s.Races.Count != command.Count
                ? $"Expected exactly {command.Count} races but got {s.Races.Count}."
                : null,
            cancellationToken);

        logger.LogDebug("GenerateRaces completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        return schema.Races.Select(r => new Race {
                WorldId = command.WorldId, Name = r.Name, Description = r.Description, CultureStyle = r.CultureStyle
            })
            .ToList();
    }
}

file class RaceListSchema {
    public List<RaceItemSchema> Races { get; set; } = [];
}

file class RaceItemSchema {
    public string CultureStyle { get; set; } = "";
    public string Description { get; set; } = "";
    public string Name { get; set; } = "";
}