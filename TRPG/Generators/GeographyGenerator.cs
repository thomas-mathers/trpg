using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Generators;

internal class GeographyGeneratorInput {
    public required string Description { get; init; }
    public int MaxRuralRegions { get; init; } = WorldGenerationDefaults.MaxRuralRegions;
    public int MaxUrbanRegions { get; init; } = WorldGenerationDefaults.MaxUrbanRegions;
    public int MaxCountries { get; init; } = WorldGenerationDefaults.MaxCountries;
    public int MinRuralRegions { get; init; } = WorldGenerationDefaults.MinRuralRegions;
    public int MinUrbanRegions { get; init; } = WorldGenerationDefaults.MinUrbanRegions;
    public int MinCountries { get; init; } = WorldGenerationDefaults.MinCountries;
    public IReadOnlyList<Race>? Races { get; init; }
    public int WorldHeight { get; init; } = WorldGenerationDefaults.WorldHeight;
    public Guid? WorldId { get; init; }
    public int WorldWidth { get; init; } = WorldGenerationDefaults.WorldWidth;
}

internal class GeographyGeneratorResult {
    public required IReadOnlyList<Region> Regions { get; init; }
    public required IReadOnlyDictionary<Guid, string> RegionFocuses { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required World World { get; init; }
}

internal class GeographyGenerator(
    OllamaApiClient client,
    ILogger<GeographyGenerator> logger) {
    private static readonly string[] RegionFocusList = [
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

    public async Task<GeographyGeneratorResult> Generate(
        GeographyGeneratorInput generatorInput,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        var numUrbanRegions = Random.Shared.Next(generatorInput.MinUrbanRegions, generatorInput.MaxUrbanRegions + 1);
        var numRuralRegions = Random.Shared.Next(generatorInput.MinRuralRegions, generatorInput.MaxRuralRegions + 1);
        var numberOfCountries = Random.Shared.Next(generatorInput.MinCountries, generatorInput.MaxCountries + 1);
        var map = MapGenerator.Generate(generatorInput.WorldWidth, generatorInput.WorldHeight, numUrbanRegions,
            numRuralRegions, numberOfCountries);
        var context = new GeographyGenerationContext(generatorInput, map, []);

        var world = await GenerateWorldEntity(context, cancellationToken);
        var countries = await GenerateCountryEntities(context, world, cancellationToken);
        var regions = await GenerateRegionEntities(new GenerateRegionsInput(context, world, countries), cancellationToken);
        var roads = await GenerateRoadEntities(new GenerateRoadsInput(context, world, regions), cancellationToken);

        logger.LogDebug("GenerateGeography completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GeographyGeneratorResult {
            World = world,
            Countries = countries.Countries,
            Regions = regions.Regions,
            Roads = roads,
            RegionFocuses = regions.RegionFocuses
        };
    }

    private async Task<World> GenerateWorldEntity(GeographyGenerationContext context,
        CancellationToken cancellationToken) {
        var worldSchema = await client.GetJson<GeographyEntitySchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
             Respond with a single JSON object with Name and Description fields. The description should capture the world's tone, culture, and character — do not reference specific geography, terrain, named places, named individuals, or specific institutions. You MUST respond in English only. Never use Chinese or any non-Latin characters. Do not use markdown.
             """,
            "Generate the world: provide its Name and Description.",
            cancellationToken: cancellationToken);

        var world = new World {
            Id = context.GeneratorInput.WorldId ?? Guid.NewGuid(),
            Name = worldSchema.Name,
            Description = worldSchema.Description,
            Boundary = new Rectangle(0, 0, context.GeneratorInput.WorldWidth, context.GeneratorInput.WorldHeight)
        };
        context.ExistingNames.Add(world.Name);
        return world;
    }

    private async Task<GeneratedCountries> GenerateCountryEntities(
        GeographyGenerationContext context,
        World world,
        CancellationToken cancellationToken
    ) {
        var countries = new List<Country>();
        for (var i = 0; i < context.Map.Countries.Count; i++) {
            var namesHint =
                $" Do not reuse any of these already-used names: {string.Join(", ", context.ExistingNames)}.";
            var schema = await client.GetJson<GeographyEntitySchema>(
                logger,
                $"""
                 You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
                 The world is {world.Name}: {world.Description}.
                 Respond with a single JSON object with Name and Description fields. The description must be a single sentence capturing the country's culture and character — no geography, named cities, individuals, or institutions. You MUST respond in English only. Never use Chinese or any non-Latin characters. Do not use markdown.
                 """,
                $"Generate country {i + 1} of {context.Map.Countries.Count}: provide its Name and Description.{namesHint}",
                s => context.ExistingNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase)
                    ? $"The name \"{s.Name}\" is already in use. Choose a different name."
                    : null,
                cancellationToken);

            countries.Add(new Country {
                WorldId = world.Id,
                Name = schema.Name,
                Description = schema.Description,
                Boundary = new Polygon { Points = new List<Point>(context.Map.Countries[i].Boundary.Points.ToArray()) }
            });
            context.ExistingNames.Add(schema.Name);
        }

        var countryById = context.Map.Countries.Zip(countries).ToDictionary(p => p.First.Id, p => p.Second);
        return new GeneratedCountries(countries, countryById);
    }

    private static Dictionary<Guid, string> AssignRegionFocuses(MapGeneratorResult map) {
        var focusMap = new Dictionary<Guid, string>();
        var urbanRegions = map.Regions.Where(r => r.RegionType == RegionType.Urban).ToList();
        for (var i = 0; i < urbanRegions.Count; i++) {
            focusMap[urbanRegions[i].Id] = RegionFocusList[i % RegionFocusList.Length];
        }

        return focusMap;
    }

    private async Task<GeneratedRegions> GenerateRegionEntities(GenerateRegionsInput input,
        CancellationToken cancellationToken) {
        var context = input.Context;
        var world = input.World;
        var countries = input.Countries;

        var focusMap = AssignRegionFocuses(context.Map);
        var mapRegionsByCountryId = context.Map.Regions
            .GroupBy(r => r.CountryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var regions = new List<Region>();
        var regionFocuses = new Dictionary<Guid, string>();
        var regionById = new Dictionary<Guid, Region>();
        var races = context.GeneratorInput.Races;
        var countryIndex = 0;

        foreach (var (countryLayoutId, country) in countries.CountryById) {
            var countryRegions = mapRegionsByCountryId.GetValueOrDefault(countryLayoutId, []);
            if (countryRegions.Count == 0) {
                continue;
            }

            var urbanRegions = countryRegions.Where(r => r.RegionType == RegionType.Urban).ToList();
            var ruralRegions = countryRegions.Where(r => r.RegionType == RegionType.Rural).ToList();

            if (urbanRegions.Count > 0) {
                var namingStyle = races is { Count: > 0 }
                    ? $"{races[countryIndex % races.Count].CultureStyle} (matching the {races[countryIndex % races.Count].Name} race who dominate this country)"
                    : NamingStyles[countryIndex % NamingStyles.Length];

                var namesHint = context.ExistingNames.Count > 0
                    ? $" Forbidden names (already used): {string.Join(", ", context.ExistingNames)}."
                    : string.Empty;

                var cityList = string.Join("\n", urbanRegions.Select((r, j) =>
                    $"{j + 1}. {focusMap[r.Id]}{(r.IsCapital ? " (capital)" : "")}"));

                var schema = await client.GetJson<CityListSchema>(
                    logger,
                    $"""
                     You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
                     The world is {world.Name}: {world.Description}.
                     Respond with a JSON object with a Cities array containing exactly {urbanRegions.Count} entries for the country of {country.Name}: {country.Description}.
                     Each entry has Name and Description. Each description must be a single sentence capturing the city's culture and character.
                     Use {namingStyle} naming conventions — invent names inspired by that culture, do not use actual historical, mythological, or fictional place names. All names must be unique and must NOT appear in the forbidden list. Never use compound portmanteau names (no "Verdantmind", "Copperstone", "Melodyshire"). You MUST respond in English only. Do not use markdown.
                     """,
                    $"Generate {urbanRegions.Count} cities for {country.Name}. Cultural focus for each (in order):\n{cityList}{namesHint}",
                    s => {
                        if (s.Cities.Count != urbanRegions.Count) {
                            return $"Expected {urbanRegions.Count} cities, got {s.Cities.Count}.";
                        }

                        var internalDupe = s.Cities
                            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault(g => g.Count() > 1)?.Key;
                        if (internalDupe != null) {
                            return $"Duplicate name \"{internalDupe}\" in the batch. All names must be unique.";
                        }

                        var crossDupe = s.Cities
                            .FirstOrDefault(c => context.ExistingNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase));
                        if (crossDupe != null) {
                            return $"The name \"{crossDupe.Name}\" is already in use. Choose a different name.";
                        }

                        return null;
                    },
                    cancellationToken);

                for (var j = 0; j < urbanRegions.Count; j++) {
                    var mapRegion = urbanRegions[j];
                    var region = new Region {
                        CountryId = country.Id,
                        Name = schema.Cities[j].Name,
                        Description = schema.Cities[j].Description,
                        Width = WorldGenerationDefaults.CityTileSize,
                        Height = WorldGenerationDefaults.CityTileSize,
                        IsCapital = mapRegion.IsCapital,
                        RegionType = RegionType.Urban,
                        Boundary = new Polygon { Points = new List<Point>(mapRegion.Boundary.Points.ToArray()) }
                    };
                    regions.Add(region);
                    regionFocuses[region.Id] = focusMap[mapRegion.Id];
                    regionById[mapRegion.Id] = region;
                    context.ExistingNames.Add(schema.Cities[j].Name);
                }
            }

            for (var j = 0; j < ruralRegions.Count; j++) {
                var mapRegion = ruralRegions[j];
                var region = new Region {
                    CountryId = country.Id,
                    Name = $"Wilderness {j + 1}",
                    Description = "An untamed wilderness region.",
                    Width = WorldGenerationDefaults.CityTileSize,
                    Height = WorldGenerationDefaults.CityTileSize,
                    IsCapital = false,
                    RegionType = RegionType.Rural,
                    Boundary = new Polygon { Points = new List<Point>(mapRegion.Boundary.Points.ToArray()) }
                };
                regions.Add(region);
                regionById[mapRegion.Id] = region;
            }

            countryIndex++;
        }

        return new GeneratedRegions(regions, regionFocuses, regionById);
    }

    private async Task<List<Road>> GenerateRoadEntities(GenerateRoadsInput input, CancellationToken cancellationToken) {
        var context = input.Context;
        var world = input.World;
        var regions = input.Regions;

        var mapRegionById = context.Map.Regions.ToDictionary(r => r.Id);
        var roadPairList = string.Join("\n", context.Map.Roads.Select((r, i) =>
            $"{i + 1}. {regions.RegionById[r.OriginRegionId].Name} and {regions.RegionById[r.DestinationRegionId].Name}"));

        var roadNamesSchema = await client.GetJson<RoadNamesSchema>(
            logger,
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
             The world is {world.Name}: {world.Description}.
             Respond with a JSON object with a Roads array containing exactly {context.Map.Roads.Count} entries. Each entry must have an Index (1-based integer matching the road number) and a Name. You MUST produce one entry per road — do not merge or skip any. Road names should be evocative and thematic — like "The King's Road", "Ember Trail", or "Merchant's Way" — not descriptions of the endpoints. All names must be unique. You MUST respond in English only. Do not use markdown.
             """,
            $"Generate names for these {context.Map.Roads.Count} roads:\n{roadPairList}",
            s => s.Roads.Count != context.Map.Roads.Count
                ? $"Expected exactly {context.Map.Roads.Count} entries but got {s.Roads.Count}. You MUST produce one entry per road number."
                : null,
            cancellationToken);

        var roadNamesByIndex = roadNamesSchema.Roads.ToDictionary(r => r.Index, r => r.Name);

        return context.Map.Roads.Select((layoutRoad, i) => {
            var originRegion = regions.RegionById[layoutRoad.OriginRegionId];
            var destRegion = regions.RegionById[layoutRoad.DestinationRegionId];
            var originCenter = mapRegionById[layoutRoad.OriginRegionId].Center;
            var destCenter = mapRegionById[layoutRoad.DestinationRegionId].Center;
            var dx = originCenter.X - destCenter.X;
            var dy = originCenter.Y - destCenter.Y;
            var distance = (float) Math.Sqrt(dx * dx + dy * dy);
            return new Road {
                Name = roadNamesByIndex.GetValueOrDefault(i + 1, $"Road {i + 1}"),
                OriginRegionId = originRegion.Id,
                DestinationRegionId = destRegion.Id,
                Distance = distance,
                TravelTime = Math.Max(1, (int) (distance / 50)),
                DangerLevel = (float) Random.Shared.NextDouble() * 0.5f
            };
        }).ToList();
    }
}

internal record GeographyGenerationContext(
    GeographyGeneratorInput GeneratorInput,
    MapGeneratorResult Map,
    List<string> ExistingNames);

internal record GeneratedCountries(List<Country> Countries, Dictionary<Guid, Country> CountryById);

internal record GeneratedRegions(
    List<Region> Regions,
    Dictionary<Guid, string> RegionFocuses,
    Dictionary<Guid, Region> RegionById);

internal record GenerateRegionsInput(GeographyGenerationContext Context, World World, GeneratedCountries Countries);

internal record GenerateRoadsInput(GeographyGenerationContext Context, World World, GeneratedRegions Regions);

file class RoadNamesSchema {
    public List<RoadNameItemSchema> Roads { get; init; } = [];
}

file class RoadNameItemSchema {
    public int Index { get; init; }
    public string Name { get; init; } = "";
}

file class CityListSchema {
    public List<CityItemSchema> Cities { get; init; } = [];
}

file class CityItemSchema {
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
}

file class GeographyEntitySchema {
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
}
