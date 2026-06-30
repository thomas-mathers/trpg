using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Generators;

internal class RaceGeneratorInput {
    public required Guid WorldId { get; init; }
    public required string Description { get; init; }
    public int Count { get; init; } = 6;
}

internal class RaceGenerator(OllamaApiClient client, ILogger<RaceGenerator> logger) {
    public async Task<IReadOnlyList<Race>> Generate(
        RaceGeneratorInput generatorInput,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var schema = await client.GetJson<RaceListSchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {generatorInput.Description}.
             Generate {generatorInput.Count} unique races. Respond with a JSON object with a Races array, each element having Name, Description, and CultureStyle.
             Each description must be a single sentence. CultureStyle must be one of: Nordic/Viking, Roman, Feudal Japanese, Arabic/Persian, Celtic, Slavic, Ancient Egyptian, Mesoamerican, Byzantine, Mongol. Each race must have a different CultureStyle. You MUST respond in English only. Do not use markdown.
             """,
            $"Generate {generatorInput.Count} unique races.",
            s => s.Races.Count != generatorInput.Count
                ? $"Expected exactly {generatorInput.Count} races but got {s.Races.Count}."
                : null,
            cancellationToken);

        logger.LogDebug("GenerateRaces completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        return schema.Races.Select(r => new Race {
                WorldId = generatorInput.WorldId, Name = r.Name, Description = r.Description, CultureStyle = r.CultureStyle
            })
            .ToList();
    }
}

file class RaceListSchema {
    public List<RaceItemSchema> Races { get; init; } = [];
}

file class RaceItemSchema {
    public string CultureStyle { get; init; } = "";
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
}
