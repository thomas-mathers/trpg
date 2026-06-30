using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Generators;

internal class FactionsGeneratorInput {
    public required Guid WorldId { get; init; }
    public required string Description { get; init; }
    public int Count { get; init; } = 4;
}

internal class FactionsGenerator(OllamaApiClient client, ILogger<FactionsGenerator> logger) {
    public async Task<IReadOnlyList<Faction>> Generate(
        FactionsGeneratorInput command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var schema = await client.GetJson<FactionListSchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             Generate {command.Count} unique factions. Respond with a JSON object with a Factions array, each element having Name and Description. Each description must be a single sentence. You MUST respond in English only. Do not use markdown.
             """,
            $"Generate {command.Count} unique factions.",
            s => s.Factions.Count != command.Count
                ? $"Expected exactly {command.Count} factions but got {s.Factions.Count}."
                : null,
            cancellationToken);

        logger.LogDebug("GenerateFactions completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        return schema.Factions.Select(f => new Faction
            { WorldId = command.WorldId, Name = f.Name, Description = f.Description }).ToList();
    }
}

file class FactionListSchema {
    public List<FactionItemSchema> Factions { get; init; } = [];
}

file class FactionItemSchema {
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
}
