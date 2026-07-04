using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Generators;

internal class GeographyGeneratorInput {
    public required string Description { get; init; }
    public int MaxCityStates { get; init; } = WorldGenerationDefaults.MaxCityStates;
    public int MaxCountries { get; init; } = WorldGenerationDefaults.MaxCountries;
    public int MaxRuralStates { get; init; } = WorldGenerationDefaults.MaxRuralStates;
    public int MinCityStates { get; init; } = WorldGenerationDefaults.MinCityStates;
    public int MinCountries { get; init; } = WorldGenerationDefaults.MinCountries;
    public int MinRuralStates { get; init; } = WorldGenerationDefaults.MinRuralStates;
    public IReadOnlyList<Race>? Races { get; init; }
    public int WorldHeight { get; init; } = WorldGenerationDefaults.WorldHeight;
    public Guid? WorldId { get; init; }
    public int WorldWidth { get; init; } = WorldGenerationDefaults.WorldWidth;
}

internal class GeographyGeneratorResult {
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyDictionary<Guid, string> CityFocuses { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required World World { get; init; }
}

internal class GeographyGenerator(
    OllamaApiClient client,
    ILogger<GeographyGenerator> logger) {
    private const int MaxCitiesPerRequest = 6;

    private static readonly string[] CityFocusList = [
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
        var numCityStates = Random.Shared.Next(generatorInput.MinCityStates, generatorInput.MaxCityStates + 1);
        var numNonCityStates = Random.Shared.Next(generatorInput.MinRuralStates, generatorInput.MaxRuralStates + 1);
        var numberOfCountries = Random.Shared.Next(generatorInput.MinCountries, generatorInput.MaxCountries + 1);
        var map = MapGenerator.Generate(generatorInput.WorldWidth, generatorInput.WorldHeight, numCityStates,
            numNonCityStates, numberOfCountries);
        var context = new GeographyGenerationContext(generatorInput, map, []);

        var world = await GenerateWorldEntity(context, cancellationToken);
        var countries = await GenerateCountryEntities(context, world, cancellationToken);
        var states = await GenerateStateEntities(new GenerateStatesInput(context, world, countries), cancellationToken);
        var roads = await GenerateRoadEntities(new GenerateRoadsInput(context, world, states), cancellationToken);

        logger.LogDebug("GenerateGeography completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GeographyGeneratorResult {
            World = world,
            Countries = countries.Countries,
            States = states.States,
            Cities = states.Cities,
            Roads = roads,
            CityFocuses = states.CityFocuses
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

    private static Dictionary<Guid, string> AssignCityFocuses(MapGeneratorResult map) {
        var focusMap = new Dictionary<Guid, string>();
        var citySites = map.States.Where(s => s.HasCity).ToList();
        for (var i = 0; i < citySites.Count; i++) {
            focusMap[citySites[i].Id] = CityFocusList[i % CityFocusList.Length];
        }

        return focusMap;
    }

    private async Task<GeneratedStates> GenerateStateEntities(GenerateStatesInput input,
        CancellationToken cancellationToken) {
        var context = input.Context;
        var world = input.World;
        var countries = input.Countries;

        var focusMap = AssignCityFocuses(context.Map);
        var mapStatesByCountryId = context.Map.States
            .GroupBy(s => s.CountryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var states = new List<State>();
        var cities = new List<City>();
        var cityFocuses = new Dictionary<Guid, string>();
        var stateById = new Dictionary<Guid, State>();
        var races = context.GeneratorInput.Races;
        var countryIndex = 0;

        foreach (var (countryLayoutId, country) in countries.CountryById) {
            var countryStates = mapStatesByCountryId.GetValueOrDefault(countryLayoutId, []);
            if (countryStates.Count == 0) {
                continue;
            }

            var cityStates = countryStates.Where(s => s.HasCity).ToList();
            var nonCityStates = countryStates.Where(s => !s.HasCity).ToList();

            if (cityStates.Count > 0) {
                var namingStyle = races is { Count: > 0 }
                    ? $"{races[countryIndex % races.Count].CultureStyle} (matching the {races[countryIndex % races.Count].Name} race who dominate this country)"
                    : NamingStyles[countryIndex % NamingStyles.Length];

                for (var chunkStart = 0; chunkStart < cityStates.Count; chunkStart += MaxCitiesPerRequest) {
                    var chunk = cityStates.Skip(chunkStart).Take(MaxCitiesPerRequest).ToList();

                    var namesHint = context.ExistingNames.Count > 0
                        ? $" Forbidden names (already used): {string.Join(", ", context.ExistingNames)}."
                        : string.Empty;

                    var cityList = string.Join("\n", chunk.Select((s, j) =>
                        $"{j + 1}. {focusMap[s.Id]}{(s.IsCapital ? " (capital)" : "")}"));

                    var schema = await client.GetJson<CityListSchema>(
                        logger,
                        $"""
                         You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
                         The world is {world.Name}: {world.Description}.
                         Respond with a JSON object with a Cities array containing exactly {chunk.Count} entries for the country of {country.Name}: {country.Description}.
                         Each entry has Name and Description. Each description must be a single sentence capturing the city's culture and character.
                         Use {namingStyle} naming conventions — invent names inspired by that culture, do not use actual historical, mythological, or fictional place names. All names must be unique and must NOT appear in the forbidden list. Never use compound portmanteau names (no "Verdantmind", "Copperstone", "Melodyshire"). You MUST respond in English only. Do not use markdown.
                         """,
                        $"Generate {chunk.Count} cities for {country.Name}. Cultural focus for each (in order):\n{cityList}{namesHint}",
                        s => {
                            if (s.Cities.Count != chunk.Count) {
                                return $"Expected {chunk.Count} cities, got {s.Cities.Count}.";
                            }

                            var internalDupe = s.Cities
                                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                                .FirstOrDefault(g => g.Count() > 1)?.Key;
                            if (internalDupe != null) {
                                return $"Duplicate name \"{internalDupe}\" in the batch. All names must be unique.";
                            }

                            var crossDupe = s.Cities
                                .FirstOrDefault(c =>
                                    context.ExistingNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase));
                            if (crossDupe != null) {
                                return $"The name \"{crossDupe.Name}\" is already in use. Choose a different name.";
                            }

                            return null;
                        },
                        cancellationToken);

                    for (var j = 0; j < chunk.Count; j++) {
                        var mapState = chunk[j];
                        var state = new State {
                            CountryId = country.Id,
                            Name = $"{schema.Cities[j].Name} Territory",
                            Description = schema.Cities[j].Description,
                            Width = WorldGenerationDefaults.CityTileSize,
                            Height = WorldGenerationDefaults.CityTileSize,
                            Center = mapState.Center,
                            Boundary = new Polygon { Points = new List<Point>(mapState.Boundary.Points.ToArray()) },
                            WorldId = world.Id
                        };
                        var city = new City {
                            StateId = state.Id,
                            CountryId = country.Id,
                            Name = schema.Cities[j].Name,
                            Description = schema.Cities[j].Description,
                            IsCapital = mapState.IsCapital,
                            WorldId = world.Id
                        };
                        states.Add(state);
                        cities.Add(city);
                        cityFocuses[city.Id] = focusMap[mapState.Id];
                        stateById[mapState.Id] = state;
                        context.ExistingNames.Add(schema.Cities[j].Name);
                    }
                }
            }

            for (var j = 0; j < nonCityStates.Count; j++) {
                var mapState = nonCityStates[j];
                var state = new State {
                    CountryId = country.Id,
                    Name = $"Wilderness {j + 1}",
                    Description = "An untamed wilderness region.",
                    Width = WorldGenerationDefaults.CityTileSize,
                    Height = WorldGenerationDefaults.CityTileSize,
                    Center = mapState.Center,
                    Boundary = new Polygon { Points = new List<Point>(mapState.Boundary.Points.ToArray()) },
                    WorldId = world.Id
                };
                states.Add(state);
                stateById[mapState.Id] = state;
            }

            countryIndex++;
        }

        return new GeneratedStates(states, cities, cityFocuses, stateById);
    }

    private async Task<List<Road>> GenerateRoadEntities(GenerateRoadsInput input, CancellationToken cancellationToken) {
        var context = input.Context;
        var world = input.World;
        var states = input.States;

        var mapStateById = context.Map.States.ToDictionary(s => s.Id);
        var roadPairList = string.Join("\n", context.Map.Roads.Select((r, i) =>
            $"{i + 1}. {states.StateById[r.OriginStateId].Name} and {states.StateById[r.DestinationStateId].Name}"));

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
            var originState = states.StateById[layoutRoad.OriginStateId];
            var destState = states.StateById[layoutRoad.DestinationStateId];
            var originCenter = mapStateById[layoutRoad.OriginStateId].Center;
            var destCenter = mapStateById[layoutRoad.DestinationStateId].Center;
            var dx = originCenter.X - destCenter.X;
            var dy = originCenter.Y - destCenter.Y;
            var distance = (float) Math.Sqrt(dx * dx + dy * dy);
            return new Road {
                Name = roadNamesByIndex.GetValueOrDefault(i + 1, $"Road {i + 1}"),
                OriginStateId = originState.Id,
                DestinationStateId = destState.Id,
                Distance = distance,
                TravelTime = Math.Max(1, (int) (distance / 50)),
                DangerLevel = (float) Random.Shared.NextDouble() * 0.5f,
                WorldId = world.Id
            };
        }).ToList();
    }
}

internal record GeographyGenerationContext(
    GeographyGeneratorInput GeneratorInput,
    MapGeneratorResult Map,
    List<string> ExistingNames);

internal record GeneratedCountries(List<Country> Countries, Dictionary<Guid, Country> CountryById);

internal record GeneratedStates(
    List<State> States,
    List<City> Cities,
    Dictionary<Guid, string> CityFocuses,
    Dictionary<Guid, State> StateById);

internal record GenerateStatesInput(GeographyGenerationContext Context, World World, GeneratedCountries Countries);

internal record GenerateRoadsInput(GeographyGenerationContext Context, World World, GeneratedStates States);

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