using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateRacesCommand {
    public int Count { get; init; } = 6;
    public required string Description { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GenerateRacesCommandHandler(AiClient client, ILogger<GenerateRacesCommandHandler> logger) {
    public async Task<IReadOnlyList<Race>> Handle(
        GenerateRacesCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        
        const string example =
            """{"Name":"Humans","Description":"Adaptable and ambitious people found across all regions."}""";
        
        var chat = client.CreateChat<GenerateRacesCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             When asked to generate a race, respond with a single JSON object with Name and Description fields only.
             Each race must be unique and different from any already generated. Do not use markdown.
             Example: {example}
             """);

        var races = new List<Race>();
        for (var i = 0; i < command.Count; i++) {
            var schema =
                await chat.GetJson<RaceSchema>($"Generate race {i + 1} of {command.Count}.", cancellationToken);
            races.Add(new Race { WorldId = command.WorldId, Name = schema.Name, Description = schema.Description });
        }

        logger.LogDebug("GenerateRaces completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        
        return races;
    }
}