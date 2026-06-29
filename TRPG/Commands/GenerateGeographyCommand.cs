using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
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
    public IReadOnlyList<Race>? Races { get; init; }
    public int WorldHeight { get; init; } = WorldGenerationDefaults.WorldHeight;
    public Guid? WorldId { get; init; }
    public int WorldWidth { get; init; } = WorldGenerationDefaults.WorldWidth;
}

internal class GenerateGeographyCommandResult {
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyDictionary<Guid, string> CityFocuses { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required World World { get; init; }
}

internal class GenerateGeographyCommandHandler(
    OllamaApiClient client,
    ILogger<GenerateGeographyCommandHandler> logger) {
    private static readonly string[] CityFocuses = [
        "trade and commerce",
        "arcane scholarship",
        "military tradition",
        "agricultural heritage",
        "artistic craftsmanship",
        "religious devotion",
        "political intrigue",
        "natural philosophy",
        "mining and industry",
        "bardic culture"
    ];

    private static readonly string[] NamingStyles = [
        "Norse (e.g., Halvard, Ironvik, Skaldmere, Dawnfjord, Ashvik)",
        "Roman (e.g., Varentum, Ostara, Caelis, Viridunum, Arvona)",
        "Celtic-inspired invented names (e.g., Dun Mhor, Carath, Briga, Valdun, Erenmor) — do NOT use real-world place names",
        "Arabic (e.g., Qadir, Zafar, Tariq, Basira, Sulkhan)",
        "Persian (e.g., Shirat, Karaj, Firuz, Ardahan, Marvast)",
        "Slavic (e.g., Vrathok, Kazan, Novik, Srebren, Mirov)",
        "Japanese-inspired (e.g., Hakurei, Tsuruga, Midori, Karaten, Shiran)"
    ];

    public async Task<GenerateGeographyCommandResult> Handle(
        GenerateGeographyCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var totalCities = Random.Shared.Next(command.MinCities, command.MaxCities + 1);
        var numberOfCountries = Random.Shared.Next(command.MinCountries, command.MaxCountries + 1);

        var map = MapGenerator.Generate(command.WorldWidth, command.WorldHeight, totalCities, numberOfCountries);

        var worldSchema = await client.GetJson<GeographyEntitySchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             Respond with a single JSON object with Name and Description fields. The description should capture the world's tone, culture, and character — do not reference specific geography, terrain, named places, named individuals, or specific institutions. You MUST respond in English only. Never use Chinese or any non-Latin characters. Do not use markdown.
             """,
            "Generate the world: provide its Name and Description.",
            cancellationToken: cancellationToken);

        var world = new World {
            Id = command.WorldId ?? Guid.NewGuid(),
            Name = worldSchema.Name,
            Description = worldSchema.Description,
            Boundary = new Rectangle(0, 0, command.WorldWidth, command.WorldHeight)
        };

        var existingNames = new List<string> { world.Name };
        var countries = new List<Country>();

        for (var i = 0; i < map.Countries.Count; i++) {
            var namesHint = $" Do not reuse any of these already-used names: {string.Join(", ", existingNames)}.";

            var schema = await client.GetJson<GeographyEntitySchema>(
                logger,
                $"""
                 You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
                 The world is {world.Name}: {world.Description}.
                 Respond with a single JSON object with Name and Description fields. The description must be a single sentence capturing the country's culture and character — no geography, named cities, individuals, or institutions. You MUST respond in English only. Never use Chinese or any non-Latin characters. Do not use markdown.
                 """,
                $"Generate country {i + 1} of {map.Countries.Count}: provide its Name and Description.{namesHint}",
                s => existingNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase)
                    ? $"The name \"{s.Name}\" is already in use. Choose a different name."
                    : null,
                cancellationToken);

            countries.Add(new Country {
                WorldId = world.Id,
                Name = schema.Name,
                Description = schema.Description,
                Boundary = new Polygon { Points = new List<Point>(map.Countries[i].Boundary.Points.ToArray()) }
            });

            existingNames.Add(schema.Name);
        }

        var countryById = map.Countries.Zip(countries).ToDictionary(p => p.First.Id, p => p.Second);
        var cities = new List<City>();
        var cityFocuses = new Dictionary<Guid, string>();

        // Pre-assign focuses by index across all cities
        var cityFocusMap = new Dictionary<Guid, string>();
        for (var i = 0; i < map.Cities.Count; i++) {
            cityFocusMap[map.Cities[i].Id] = CityFocuses[i % CityFocuses.Length];
        }

        // Batch city generation per country
        var mapCitiesByCountryId = map.Cities
            .GroupBy(c => c.CountryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var races = command.Races;
        var countryIndex = 0;
        foreach (var (countryLayoutId, country) in countryById) {
            var countryCities = mapCitiesByCountryId.GetValueOrDefault(countryLayoutId, []);
            if (countryCities.Count == 0) {
                continue;
            }

            var namingStyle = races is { Count: > 0 }
                ? $"{races[countryIndex % races.Count].CultureStyle} (matching the {races[countryIndex % races.Count].Name} race who dominate this country)"
                : NamingStyles[countryIndex % NamingStyles.Length];
            countryIndex++;

            var namesHint = existingNames.Count > 0
                ? $" Forbidden names (already used): {string.Join(", ", existingNames)}."
                : string.Empty;

            var cityList = string.Join("\n", countryCities.Select((c, j) =>
                $"{j + 1}. {cityFocusMap[c.Id]}{(c.IsCapital ? " (capital)" : "")}"));

            var schema = await client.GetJson<CityListSchema>(
                logger,
                $"""
                 You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
                 The world is {world.Name}: {world.Description}.
                 Respond with a JSON object with a Cities array containing exactly {countryCities.Count} entries for the country of {country.Name}: {country.Description}.
                 Each entry has Name and Description. Each description must be a single sentence capturing the city's culture and character.
                 Use {namingStyle} naming conventions — invent names inspired by that culture, do not use actual historical, mythological, or fictional place names. All names must be unique and must NOT appear in the forbidden list. Never use compound portmanteau names (no "Verdantmind", "Copperstone", "Melodyshire"). You MUST respond in English only. Do not use markdown.
                 """,
                $"Generate {countryCities.Count} cities for {country.Name}. Cultural focus for each (in order):\n{cityList}{namesHint}",
                s => {
                    if (s.Cities.Count != countryCities.Count) {
                        return $"Expected {countryCities.Count} cities, got {s.Cities.Count}.";
                    }

                    var internalDupe = s.Cities
                        .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault(g => g.Count() > 1)?.Key;
                    if (internalDupe != null) {
                        return $"Duplicate name \"{internalDupe}\" in the batch. All names must be unique.";
                    }

                    var crossDupe = s.Cities
                        .FirstOrDefault(c => existingNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase));
                    if (crossDupe != null) {
                        return $"The name \"{crossDupe.Name}\" is already in use. Choose a different name.";
                    }

                    return null;
                },
                cancellationToken);

            for (var j = 0; j < countryCities.Count; j++) {
                var mapCity = countryCities[j];
                var city = new City {
                    CountryId = country.Id,
                    Name = schema.Cities[j].Name,
                    Description = schema.Cities[j].Description,
                    Width = WorldGenerationDefaults.CityTileSize,
                    Height = WorldGenerationDefaults.CityTileSize,
                    IsCapital = mapCity.IsCapital,
                    Boundary = new Polygon { Points = new List<Point>(mapCity.Boundary.Points.ToArray()) }
                };
                cities.Add(city);
                cityFocuses[city.Id] = cityFocusMap[mapCity.Id];
                existingNames.Add(schema.Cities[j].Name);
            }
        }

        var mapCityById = map.Cities.ToDictionary(c => c.Id);
        var cityById = map.Cities.Zip(cities).ToDictionary(p => p.First.Id, p => p.Second);

        var roadPairList = string.Join("\n", map.Roads.Select((r, i) =>
            $"{i + 1}. {cityById[r.OriginCityId].Name} and {cityById[r.DestinationCityId].Name}"));

        var roadNamesSchema = await client.GetJson<RoadNamesSchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             The world is {world.Name}: {world.Description}.
             Respond with a JSON object with a Roads array containing exactly {map.Roads.Count} entries. Each entry must have an Index (1-based integer matching the road number) and a Name. You MUST produce one entry per road — do not merge or skip any. Road names should be evocative and thematic — like "The King's Road", "Ember Trail", or "Merchant's Way" — not descriptions of the endpoints. All names must be unique. You MUST respond in English only. Do not use markdown.
             """,
            $"Generate names for these {map.Roads.Count} roads:\n{roadPairList}",
            s => s.Roads.Count != map.Roads.Count
                ? $"Expected exactly {map.Roads.Count} entries but got {s.Roads.Count}. You MUST produce one entry per road number."
                : null,
            cancellationToken);

        var roadNamesByIndex = roadNamesSchema.Roads.ToDictionary(r => r.Index, r => r.Name);

        var roads = map.Roads.Select((layoutRoad, i) => {
            var originCity = cityById[layoutRoad.OriginCityId];
            var destCity = cityById[layoutRoad.DestinationCityId];
            var originCenter = mapCityById[layoutRoad.OriginCityId].Center;
            var destCenter = mapCityById[layoutRoad.DestinationCityId].Center;
            var dx = originCenter.X - destCenter.X;
            var dy = originCenter.Y - destCenter.Y;
            var distance = (float) Math.Sqrt(dx * dx + dy * dy);
            return new Road {
                Name = roadNamesByIndex.GetValueOrDefault(i + 1, $"Road {i + 1}"),
                OriginCityId = originCity.Id,
                DestinationCityId = destCity.Id,
                Distance = distance,
                TravelTime = Math.Max(1, (int) (distance / 50)),
                DangerLevel = (float) Random.Shared.NextDouble() * 0.5f
            };
        }).ToList();

        logger.LogDebug("GenerateGeography completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GenerateGeographyCommandResult {
            World = world,
            Countries = countries,
            Cities = cities,
            Roads = roads,
            CityFocuses = cityFocuses
        };
    }
}

file class RoadNamesSchema {
    public List<RoadNameItemSchema> Roads { get; set; } = [];
}

file class RoadNameItemSchema {
    public int Index { get; set; }
    public string Name { get; set; } = "";
}

file class CityListSchema {
    public List<CityItemSchema> Cities { get; set; } = [];
}

file class CityItemSchema {
    public string Description { get; set; } = "";
    public string Name { get; set; } = "";
}