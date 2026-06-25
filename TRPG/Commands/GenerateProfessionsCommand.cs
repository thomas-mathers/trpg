using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateProfessionsCommand {
    public int Count { get; init; } = 8;
    public required string Description { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GenerateProfessionsCommandHandler(AiClient client, ILogger<GenerateProfessionsCommandHandler> logger) {
    public async Task<IReadOnlyList<Profession>> Handle(
        GenerateProfessionsCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        
        const string example =
            """{"Name":"Blacksmith","Description":"A skilled metalworker who forges weapons, armour, and tools."}""";
        
        var chat = client.CreateChat<GenerateProfessionsCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             When asked to generate a profession, respond with a single JSON object with Name and Description fields only.
             Each profession must be unique and different from any already generated. Do not use markdown.
             Example: {example}
             """);

        var professions = new List<Profession>();
        for (var i = 0; i < command.Count; i++) {
            var schema = await chat.GetJson<ProfessionSchema>($"Generate profession {i + 1} of {command.Count}.",
                cancellationToken);
            professions.Add(new Profession
                { WorldId = command.WorldId, Name = schema.Name, Description = schema.Description });
        }

        logger.LogDebug("GenerateProfessions completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        
        return professions;
    }
}