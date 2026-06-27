using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Algorithms;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateGeographyCommand {
    public required string Description { get; init; }
    public int MaxCities { get; init; } = WorldGenerationDefaults.MaxCities;
    public int MaxCountries { get; init; } = WorldGenerationDefaults.MaxCountries;
    public int MinCities { get; init; } = WorldGenerationDefaults.MinCities;
    public int MinCountries { get; init; } = WorldGenerationDefaults.MinCountries;
    public int WorldHeight { get; init; } = WorldGenerationDefaults.WorldHeight;
    public int WorldWidth { get; init; } = WorldGenerationDefaults.WorldWidth;
}

internal class GenerateGeographyCommandResult {
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required World World { get; init; }
}

internal class GenerateGeographyCommandHandler(AiClient client, ILogger<GenerateGeographyCommandHandler> logger) {
    public async Task<GenerateGeographyCommandResult> Handle(
        GenerateGeographyCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var totalCities = Random.Shared.Next(command.MinCities, command.MaxCities + 1);
        var numberOfCountries = Random.Shared.Next(command.MinCountries, command.MaxCountries + 1);

        var map = MapGenerator.Generate(command.WorldWidth, command.WorldHeight, totalCities, numberOfCountries);

        var chat = client.CreateChat<GenerateGeographyCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             Respond to each request with a single JSON object containing only the fields asked for.
             All names must be globally unique across the entire world. Do not use markdown.
             """);

        var worldSchema = await chat.GetJson<GeographyEntitySchema>(
            "Generate the world: provide its Name and Description.",
            cancellationToken);
        
        var world = new World {
            Name = worldSchema.Name,
            Description = worldSchema.Description,
            Boundary = new Rectangle(0, 0, command.WorldWidth, command.WorldHeight)
        };

        var countries = new List<Country>();
        
        for (var i = 0; i < map.Countries.Count; i++) {
            var schema = await chat.GetJson<GeographyEntitySchema>(
                $"Generate country {i + 1} of {map.Countries.Count}: provide its Name and Description.",
                cancellationToken);
            
            countries.Add(new Country {
                WorldId = world.Id,
                Name = schema.Name,
                Description = schema.Description,
                Boundary = new Polygon { Points = new List<Point>(map.Countries[i].Boundary.Points.ToArray()) }
            });
        }

        var countryById = map.Countries.Zip(countries).ToDictionary(p => p.First.Id, p => p.Second);

        var cities = new List<City>();
        
        for (var i = 0; i < map.Cities.Count; i++) {
            var mapCity = map.Cities[i];
            var country = countryById[mapCity.CountryId];
            
            var schema = await chat.GetJson<GeographyEntitySchema>(
                $"Generate city {i + 1} of {map.Cities.Count} in country {country.Name}: provide its Name and Description.",
                cancellationToken);
            
            cities.Add(new City {
                CountryId = country.Id,
                Name = schema.Name,
                Description = schema.Description,
                Width = WorldGenerationDefaults.CityTileSize,
                Height = WorldGenerationDefaults.CityTileSize,
                Boundary = new Polygon { Points = new List<Point>(mapCity.Boundary.Points.ToArray()) }
            });
        }

        var mapCityById = map.Cities.ToDictionary(c => c.Id);
        var cityById = map.Cities.Zip(cities).ToDictionary(p => p.First.Id, p => p.Second);

        var roads = new List<Road>();
        
        foreach (var layoutRoad in map.Roads) {
            var originCity = cityById[layoutRoad.OriginCityId];
            var destCity = cityById[layoutRoad.DestinationCityId];

            var originCityCenter = mapCityById[layoutRoad.OriginCityId].Center;
            var destinationCityCenter = mapCityById[layoutRoad.DestinationCityId].Center;
            
            var dx = originCityCenter.X - destinationCityCenter.X;
            var dy = originCityCenter.Y - destinationCityCenter.Y;
            
            var distance = (float) Math.Sqrt(dx * dx + dy * dy);

            var schema = await chat.GetJson<RoadNameSchema>(
                $"Generate a name for a road from {originCity.Name} to {destCity.Name}.",
                cancellationToken);

            roads.Add(new Road {
                Name = schema.Name,
                OriginCityId = originCity.Id,
                DestinationCityId = destCity.Id,
                Distance = distance,
                TravelTime = Math.Max(1, (int)(distance / 50)),
                DangerLevel = (float)Random.Shared.NextDouble() * 0.5f
            });
        }

        logger.LogDebug("GenerateGeography completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GenerateGeographyCommandResult {
            World = world,
            Countries = countries,
            Cities = cities,
            Roads = roads
        };
    }
}
