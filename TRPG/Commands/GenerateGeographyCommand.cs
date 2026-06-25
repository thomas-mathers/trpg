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
    public int MaxProvinces { get; init; } = 4;
    public int MinCities { get; init; } = 5;
    public int MinProvinces { get; init; } = 2;
}

internal class GenerateGeographyCommandResult {
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Province> Provinces { get; init; }
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
        var world = new World { Name = worldSchema.Name, Description = worldSchema.Description };

        var countries = new List<Country>();
        var provinces = new List<Province>();
        var cities = new List<City>();

        for (var i = 0; i < command.CountryCount; i++) {
            var countrySchema = await chat.GetJson<GeographyEntitySchema>(
                $"Generate country {i + 1} of {command.CountryCount}: provide its Name and Description.",
                cancellationToken
            );
            var country = new Country
                { WorldId = world.Id, Name = countrySchema.Name, Description = countrySchema.Description };
            countries.Add(country);

            var provinceCount = Random.Shared.Next(command.MinProvinces, command.MaxProvinces + 1);
            for (var j = 0; j < provinceCount; j++) {
                var provinceSchema = await chat.GetJson<GeographyEntitySchema>(
                    $"Generate province {j + 1} of {provinceCount} for country {country.Name}: provide its Name and Description.",
                    cancellationToken
                );
                var province = new Province
                    { CountryId = country.Id, Name = provinceSchema.Name, Description = provinceSchema.Description };
                provinces.Add(province);

                var cityCount = Random.Shared.Next(command.MinCities, command.MaxCities + 1);
                for (var k = 0; k < cityCount; k++) {
                    var citySchema = await chat.GetJson<GeographyCitySchema>(
                        $"Generate city {k + 1} of {cityCount} for province {province.Name}: provide its Name, Description, Width, and Height (size in tiles, 20-100).",
                        cancellationToken
                    );
                    var width = citySchema.Width;
                    var height = citySchema.Height;
                    cities.Add(new City {
                        ProvinceId = province.Id,
                        Name = citySchema.Name,
                        Description = citySchema.Description,
                        Width = width,
                        Height = height,
                        Boundary = new Circle {
                            Center = new Location { Coordinates = new Point(0, 0) },
                            Radius = Math.Max(width, height) / 2.0f
                        }
                    });
                }
            }
        }

        logger.LogDebug("GenerateGeography completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GenerateGeographyCommandResult {
            World = world,
            Countries = countries,
            Provinces = provinces,
            Cities = cities
        };
    }
}