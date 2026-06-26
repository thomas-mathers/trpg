using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateGeographyCommand {
    public int CountryCount { get; init; } = 3;
    public required string Description { get; init; }
    public int MaxCities { get; init; } = 8;
    public int MinCities { get; init; } = 5;
}

internal class GenerateGeographyCommandResult {
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required World World { get; init; }
}

internal class GenerateGeographyCommandHandler(AiClient client, ILogger<GenerateGeographyCommandHandler> logger) {
    public async Task<GenerateGeographyCommandResult> Handle(
        GenerateGeographyCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        
        var chat = client.CreateChat<GenerateGeographyCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             Respond to each request with a single JSON object containing only the fields asked for.
             All names must be globally unique across the entire world. Do not use markdown.
             """);

        var worldSchema = await chat.GetJson<GeographyEntitySchema>(
            "Generate the world: provide its Name and Description.",
            cancellationToken
        );
        var world = new World { Name = worldSchema.Name, Description = worldSchema.Description, Boundary = new Rectangle(0, 0, 0, 0) };

        var countries = new List<Country>();
        var cities = new List<City>();

        for (var i = 0; i < command.CountryCount; i++) {
            var countrySchema = await chat.GetJson<GeographyEntitySchema>(
                $"Generate country {i + 1} of {command.CountryCount}: provide its Name and Description.",
                cancellationToken
            );
            var country = new Country {
                WorldId = world.Id,
                Name = countrySchema.Name,
                Description = countrySchema.Description,
                Boundary = new Rectangle(0, 0, 0, 0)
            };
            countries.Add(country);

            var cityCount = Random.Shared.Next(command.MinCities, command.MaxCities + 1);
            for (var k = 0; k < cityCount; k++) {
                var citySchema = await chat.GetJson<GeographyCitySchema>(
                    $"Generate city {k + 1} of {cityCount} for country {country.Name}: provide its Name, Description, Width, and Height (size in tiles, 20-100).",
                    cancellationToken
                );
                cities.Add(new City {
                    CountryId = country.Id,
                    Name = citySchema.Name,
                    Description = citySchema.Description,
                    Width = citySchema.Width,
                    Height = citySchema.Height,
                    Boundary = new Rectangle(0, 0, 0, 0)
                });
            }
        }

        logger.LogDebug("GenerateGeography completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GenerateGeographyCommandResult {
            World = world,
            Countries = countries,
            Cities = cities
        };
    }
}