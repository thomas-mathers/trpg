using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Application.Extensions;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class GeographyGeneratorInput
{
    public required string Description { get; init; }
    public int MaxCityStates { get; init; }
    public int MaxRuralStates { get; init; }
    public int MinCityStates { get; init; }
    public int MinRuralStates { get; init; }
    public int WorldHeight { get; init; } = 10000;
    public Guid? WorldId { get; init; }
    public int WorldWidth { get; init; } = 10000;
}

public class GeographyGeneratorResult
{
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyDictionary<Guid, CreatureType> DominantRaceByCountryId { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required World World { get; init; }
}

public class GeographyGenerator(IOllamaApiClient client, ILogger<GeographyGenerator> logger)
{
    private const int MaxCitiesPerRequest = 6;
    private const int CityTileSize = 100;

    public async Task<GeographyGeneratorResult> Generate(
        GeographyGeneratorInput generatorInput,
        CancellationToken cancellationToken
    )
    {
        var numberOfCountries = CreatureTypes.Humanoid.Count;
        if (generatorInput.MinCityStates + generatorInput.MinRuralStates < numberOfCountries)
        {
            throw new InvalidOperationException(
                $"MinCityStates ({generatorInput.MinCityStates}) + MinRuralStates ({generatorInput.MinRuralStates}) must be at least {numberOfCountries} — every country (one per playable race) needs at least one state."
            );
        }

        var sw = Stopwatch.StartNew();
        var numCityStates = Random.Shared.Next(
            generatorInput.MinCityStates,
            generatorInput.MaxCityStates + 1
        );
        var numNonCityStates = Random.Shared.Next(
            generatorInput.MinRuralStates,
            generatorInput.MaxRuralStates + 1
        );
        var map = MapGenerator.Generate(
            generatorInput.WorldWidth,
            generatorInput.WorldHeight,
            numCityStates,
            numNonCityStates,
            numberOfCountries
        );
        var context = new GeographyGenerationContext(generatorInput, map, []);

        var world = await GenerateWorldEntity(context, cancellationToken);
        var countries = await GenerateCountryEntities(context, world, cancellationToken);
        var states = await GenerateStateEntities(
            new GenerateStatesInput(context, world, countries),
            cancellationToken
        );
        var roads = GenerateRoadEntities(
            new GenerateRoadsInput(context, world, states, countries.DominantRaceByCountryId)
        );

        logger.LogDebug(
            "GenerateGeography completed in {ElapsedSeconds:F1}s",
            sw.Elapsed.TotalSeconds
        );

        return new GeographyGeneratorResult
        {
            World = world,
            Countries = countries.Countries,
            States = states.States,
            Cities = states.Cities,
            Districts = states.Districts,
            Roads = roads,
            DominantRaceByCountryId = countries.DominantRaceByCountryId,
        };
    }

    private async Task<World> GenerateWorldEntity(
        GeographyGenerationContext context,
        CancellationToken cancellationToken
    )
    {
        var worldSchema = await client.GetJson<GeographyEntitySchema>(
            logger,
            $"""
            You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
            Respond with a single JSON object with Name and Description fields. The description should capture the world's tone, culture, and character — do not reference specific geography, terrain, named places, named individuals, or specific institutions. You MUST respond in English only. Never use Chinese or any non-Latin characters. Do not use markdown.
            """,
            "Generate the world: provide its Name and Description.",
            cancellationToken: cancellationToken
        );

        var world = new World
        {
            Id = context.GeneratorInput.WorldId ?? Guid.NewGuid(),
            Name = worldSchema.Name,
            Description = worldSchema.Description,
            Boundary = new Rectangle(
                0,
                0,
                context.GeneratorInput.WorldWidth,
                context.GeneratorInput.WorldHeight
            ),
        };
        context.ExistingNames.Add(world.Name);
        return world;
    }

    private static readonly Dictionary<CountryFocus, string> FocusDescriptions = new()
    {
        [CountryFocus.Scientific] = "scientific and magical pursuits",
        [CountryFocus.Political] = "political power and bureaucracy",
        [CountryFocus.Religious] = "religious devotion",
        [CountryFocus.Militaristic] = "martial strength and conquest",
    };

    private async Task<GeneratedCountries> GenerateCountryEntities(
        GeographyGenerationContext context,
        World world,
        CancellationToken cancellationToken
    )
    {
        var shuffledRaces = CreatureTypes.Humanoid.ToArray();
        Random.Shared.Shuffle(shuffledRaces);
        var focuses = Enum.GetValues<CountryFocus>();

        var countries = new List<Country>();
        for (var i = 0; i < context.Map.Countries.Count; i++)
        {
            var dominantRace = shuffledRaces[i];
            var focus = focuses[Random.Shared.Next(focuses.Length)];
            var focusDescription = FocusDescriptions[focus];
            var namesHint =
                $" Do not reuse any of these already-used names: {string.Join(", ", context.ExistingNames)}.";
            var schema = await client.GetJson<GeographyEntitySchema>(
                logger,
                $"""
                You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
                The world is {world.Name}: {world.Description}.
                Respond with a single JSON object with Name and Description fields. The description must be a single sentence capturing the country's culture and character, reflecting that its people are predominantly {dominantRace} and that its society is oriented around {focusDescription} — no geography, named cities, individuals, or institutions. You MUST respond in English only. Never use Chinese or any non-Latin characters. Do not use markdown.
                """,
                $"Generate country {i + 1} of {context.Map.Countries.Count}, whose people are predominantly {dominantRace} and whose society is oriented around {focusDescription}: provide its Name and Description.{namesHint}",
                s =>
                    context.ExistingNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase)
                        ? $"The name \"{s.Name}\" is already in use. Choose a different name."
                        : null,
                cancellationToken
            );

            var country = new Country
            {
                WorldId = world.Id,
                Name = schema.Name,
                Description = schema.Description,
                Boundary = new Polygon
                {
                    Points = new List<Point>(context.Map.Countries[i].Boundary.Points.ToArray()),
                },
                DominantRace = dominantRace,
                Focus = focus,
            };
            countries.Add(country);
            context.ExistingNames.Add(schema.Name);
        }

        var countryById = context
            .Map.Countries.Zip(countries)
            .ToDictionary(p => p.First.Id, p => p.Second);
        var dominantRaceByCountryId = countries.ToDictionary(c => c.Id, c => c.DominantRace);
        return new GeneratedCountries(countries, countryById, dominantRaceByCountryId);
    }

    private static readonly DistrictType[] RequiredDistrictTypes =
    [
        DistrictType.Residential,
        DistrictType.CityCenter,
    ];

    private static readonly DistrictType[] OptionalDistrictTypes =
    [
        DistrictType.Scientific,
        DistrictType.Governmental,
        DistrictType.HolySite,
        DistrictType.Encampment,
    ];

    private const double OptionalDistrictChance = 0.55;
    private const double FocusedDistrictChance = 0.85;

    private static readonly Dictionary<CountryFocus, DistrictType> FocusDistrictTypes = new()
    {
        [CountryFocus.Scientific] = DistrictType.Scientific,
        [CountryFocus.Political] = DistrictType.Governmental,
        [CountryFocus.Religious] = DistrictType.HolySite,
        [CountryFocus.Militaristic] = DistrictType.Encampment,
    };

    private static List<DistrictType> SelectDistrictTypes(bool isCapital, CountryFocus focus)
    {
        var focusedDistrictType = FocusDistrictTypes[focus];
        var selected = new List<DistrictType>(RequiredDistrictTypes);
        foreach (var districtType in OptionalDistrictTypes)
        {
            if (districtType == DistrictType.Governmental && isCapital)
            {
                selected.Add(districtType);
                continue;
            }

            var chance =
                districtType == focusedDistrictType
                    ? FocusedDistrictChance
                    : OptionalDistrictChance;
            if (Random.Shared.NextDouble() < chance)
            {
                selected.Add(districtType);
            }
        }

        return selected;
    }

    private async Task<GeneratedStates> GenerateStateEntities(
        GenerateStatesInput input,
        CancellationToken cancellationToken
    )
    {
        var context = input.Context;
        var world = input.World;
        var countries = input.Countries;

        var mapStatesByCountryId = context
            .Map.States.GroupBy(s => s.CountryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var states = new List<State>();
        var cities = new List<City>();
        var districts = new List<District>();
        var stateById = new Dictionary<Guid, State>();

        foreach (var (countryLayoutId, country) in countries.CountryById)
        {
            var countryStates = mapStatesByCountryId.GetValueOrDefault(countryLayoutId, []);
            if (countryStates.Count == 0)
            {
                continue;
            }

            var dominantRace = countries.DominantRaceByCountryId[country.Id];
            var cityStates = countryStates.Where(s => s.HasCity).ToList();
            var nonCityStates = countryStates.Where(s => !s.HasCity).ToList();

            if (cityStates.Count > 0)
            {
                var cityNames = cityStates
                    .Select(_ =>
                        SettlementNameGenerator.GenerateCityName(
                            dominantRace,
                            context.ExistingNames
                        )
                    )
                    .ToList();
                var cityDistrictTypes = cityStates
                    .Select(s => SelectDistrictTypes(s.IsCapital, country.Focus))
                    .ToList();

                for (
                    var chunkStart = 0;
                    chunkStart < cityStates.Count;
                    chunkStart += MaxCitiesPerRequest
                )
                {
                    var chunk = cityStates.Skip(chunkStart).Take(MaxCitiesPerRequest).ToList();
                    var chunkNames = cityNames.Skip(chunkStart).Take(MaxCitiesPerRequest).ToList();
                    var chunkDistrictTypes = cityDistrictTypes
                        .Skip(chunkStart)
                        .Take(MaxCitiesPerRequest)
                        .ToList();

                    var cityList = string.Join(
                        "\n",
                        chunk.Select(
                            (s, j) =>
                                $"{j + 1}. {chunkNames[j]}{(s.IsCapital ? " (capital)" : "")}, districts: {string.Join(", ", chunkDistrictTypes[j])}"
                        )
                    );

                    var schema = await client.GetJson<CityDescriptionListSchema>(
                        logger,
                        $"""
                        You are a creative world-building assistant for a TRPG game generating content for: {context.GeneratorInput.Description}.
                        The world is {world.Name}: {world.Description}.
                        Respond with a JSON object with a Cities array containing exactly {chunk.Count} entries for the country of {country.Name}: {country.Description}, whose people are predominantly {dominantRace} and whose society is oriented around {FocusDescriptions[
                            country.Focus
                        ]}.
                        Each entry has an Index (1-based integer matching the city number below) and a Description — a single sentence capturing that city's culture and character, consistent with its name and the districts it actually has (a city with no Scientific district isn't a center of learning; one with an Encampment district has a military presence; a Governmental district signals a seat of local power; etc). Do not invent a different name for the city. You MUST respond in English only. Do not use markdown.
                        """,
                        $"Generate descriptions for these {chunk.Count} cities:\n{cityList}",
                        s =>
                            s.Cities.Count != chunk.Count
                                ? $"Expected exactly {chunk.Count} entries but got {s.Cities.Count}. You MUST produce one entry per city number."
                                : null,
                        cancellationToken
                    );

                    var descriptionByIndex = schema.Cities.ToDictionary(
                        c => c.Index,
                        c => c.Description
                    );

                    for (var j = 0; j < chunk.Count; j++)
                    {
                        var mapState = chunk[j];
                        var cityName = chunkNames[j];
                        var description = descriptionByIndex.GetValueOrDefault(
                            j + 1,
                            $"A settlement with a {string.Join(", ", chunkDistrictTypes[j])} presence."
                        );
                        var state = new State
                        {
                            CountryId = country.Id,
                            Name = $"{cityName} Territory",
                            Description = $"The territory surrounding {cityName}.",
                            Width = CityTileSize,
                            Height = CityTileSize,
                            Center = mapState.Center,
                            Boundary = new Polygon
                            {
                                Points = new List<Point>(mapState.Boundary.Points.ToArray()),
                            },
                            WorldId = world.Id,
                        };
                        var city = new City
                        {
                            StateId = state.Id,
                            CountryId = country.Id,
                            Name = cityName,
                            Description = description,
                            IsCapital = mapState.IsCapital,
                            WorldId = world.Id,
                        };
                        states.Add(state);
                        cities.Add(city);
                        stateById[mapState.Id] = state;

                        foreach (var districtType in chunkDistrictTypes[j])
                        {
                            districts.Add(
                                DistrictGenerator.Generate(districtType, city.Id, world.Id)
                            );
                        }
                    }
                }
            }

            for (var j = 0; j < nonCityStates.Count; j++)
            {
                var mapState = nonCityStates[j];
                var state = new State
                {
                    CountryId = country.Id,
                    Name = $"Wilderness {j + 1}",
                    Description = "An untamed wilderness region.",
                    Width = CityTileSize,
                    Height = CityTileSize,
                    Center = mapState.Center,
                    Boundary = new Polygon
                    {
                        Points = new List<Point>(mapState.Boundary.Points.ToArray()),
                    },
                    WorldId = world.Id,
                };
                states.Add(state);
                stateById[mapState.Id] = state;
            }
        }

        return new GeneratedStates(states, cities, districts, stateById);
    }

    private static List<Road> GenerateRoadEntities(GenerateRoadsInput input)
    {
        var context = input.Context;
        var world = input.World;
        var states = input.States;
        var usedRoadNames = new HashSet<string>();

        var mapStateById = context.Map.States.ToDictionary(s => s.Id);

        return context
            .Map.Roads.Select(layoutRoad =>
            {
                var originState = states.StateById[layoutRoad.OriginStateId];
                var destState = states.StateById[layoutRoad.DestinationStateId];
                var originCenter = mapStateById[layoutRoad.OriginStateId].Center;
                var destCenter = mapStateById[layoutRoad.DestinationStateId].Center;
                var dx = originCenter.X - destCenter.X;
                var dy = originCenter.Y - destCenter.Y;
                var distance = (float)Math.Sqrt(dx * dx + dy * dy);
                var dominantRace = input.DominantRaceByCountryId.GetValueOrDefault(
                    originState.CountryId,
                    CreatureType.Human
                );
                return new Road
                {
                    Name = SettlementNameGenerator.GenerateRoadName(dominantRace, usedRoadNames),
                    OriginStateId = originState.Id,
                    DestinationStateId = destState.Id,
                    Distance = distance,
                    TravelTime = Math.Max(1, (int)(distance / 50)),
                    DangerLevel = (float)Random.Shared.NextDouble() * 0.5f,
                    WorldId = world.Id,
                };
            })
            .ToList();
    }
}

internal record GeographyGenerationContext(
    GeographyGeneratorInput GeneratorInput,
    MapGeneratorResult Map,
    HashSet<string> ExistingNames
);

internal record GeneratedCountries(
    List<Country> Countries,
    Dictionary<Guid, Country> CountryById,
    Dictionary<Guid, CreatureType> DominantRaceByCountryId
);

internal record GeneratedStates(
    List<State> States,
    List<City> Cities,
    List<District> Districts,
    Dictionary<Guid, State> StateById
);

internal record GenerateStatesInput(
    GeographyGenerationContext Context,
    World World,
    GeneratedCountries Countries
);

internal record GenerateRoadsInput(
    GeographyGenerationContext Context,
    World World,
    GeneratedStates States,
    IReadOnlyDictionary<Guid, CreatureType> DominantRaceByCountryId
);

internal class CityDescriptionListSchema
{
    public List<CityDescriptionItemSchema> Cities { get; init; } = [];
}

internal class CityDescriptionItemSchema
{
    public string Description { get; init; } = "";
    public int Index { get; init; }
}

internal class GeographyEntitySchema
{
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
}
