using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateFactionsCommand {
    public int Count { get; init; } = 4;
    public required string Description { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GenerateFactionsCommandHandler(AiClient client, ILogger<GenerateFactionsCommandHandler> logger) {
    public async Task<IReadOnlyList<Faction>> Handle(
        GenerateFactionsCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        
        const string example =
            """{"Name":"The Iron Compact","Description":"A powerful guild of merchants controlling trade routes across the region."}""";
        
        var chat = client.CreateChat<GenerateFactionsCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             When asked to generate a faction, respond with a single JSON object with Name and Description fields only.
             Each faction must be unique and different from any already generated. Do not use markdown.
             Example: {example}
             """);

        var factions = new List<Faction>();
        
        for (var i = 0; i < command.Count; i++) {
            var schema =
                await chat.GetJson<FactionSchema>($"Generate faction {i + 1} of {command.Count}.", cancellationToken);
            factions.Add(new Faction {
                WorldId = command.WorldId, 
                Name = schema.Name, 
                Description = schema.Description
            });
        }

        logger.LogDebug("GenerateFactions completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        return factions;
    }
}