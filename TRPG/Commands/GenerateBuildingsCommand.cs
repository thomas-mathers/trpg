using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateBuildingsCommand {
    public required City City { get; init; }
    public int Count { get; init; } = 8;
    public required string Description { get; init; }
    public IReadOnlyList<string> ExistingBuildingNames { get; init; } = [];
}

internal class GenerateBuildingsCommandHandler(AiClient client, ILogger<GenerateBuildingsCommandHandler> logger) {
    public async Task<IReadOnlyList<Building>> Handle(
        GenerateBuildingsCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        
        const string example =
            """{"Name":"The Rusty Anvil","Description":"A busy smithy known for quality ironwork and fair prices.","BuildingType":"Blacksmith"}""";

        var buildingTypes = string.Join(", ", Enum.GetNames<BuildingType>());

        var existingNamesHint = command.ExistingBuildingNames.Count > 0
            ? $"Do not reuse any of these names already used in other cities: {string.Join(", ", command.ExistingBuildingNames)}."
            : string.Empty;

        var chat = client.CreateChat<GenerateBuildingsCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             You are generating buildings for the city of {command.City.Name}: {command.City.Description}.
             When asked to generate a building, respond with a single JSON object with Name, Description, and BuildingType fields.
             BuildingType must be one of: {buildingTypes}.
             Each building must be unique and fitting for this city. {existingNamesHint} Do not use markdown.
             Example: {example}
             """);

        var buildings = new List<Building>();
        for (var i = 0; i < command.Count; i++) {
            var schema =
                await chat.GetJson<BuildingSchema>($"Generate building {i + 1} of {command.Count}.", cancellationToken);
            buildings.Add(new Building {
                CityId = command.City.Id,
                Name = schema.Name,
                Description = schema.Description,
                BuildingType = Enum.TryParse<BuildingType>(schema.BuildingType, out var buildingType)
                    ? buildingType
                    : BuildingType.House,
                Boundary = new Rectangle(0, 0, 0, 0)
            });
        }

        logger.LogDebug("GenerateBuildings({City}) completed in {ElapsedSeconds:F1}s", command.City.Name,
            sw.Elapsed.TotalSeconds);
        return buildings;
    }
}