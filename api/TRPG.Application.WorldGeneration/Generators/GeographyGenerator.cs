using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal class GeographyGeneratorInput
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

internal class GeographyGeneratorResult
{
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyDictionary<Guid, CreatureType> DominantRaceByCountryId { get; init; }
    public required IReadOnlyList<Location> Locations { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<LocationConnector> LocationConnectors { get; init; }
    public required IReadOnlyList<StateTravelLink> StateTravelLinks { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required World World { get; init; }
}

public class GeographyGenerator(
    [FromKeyedServices(LlmRoleKeys.WorldGeneration)] IChatClient client,
    ILogger<GeographyGenerator> logger
)
{
    private const int CityTileSize = 100;

    internal async Task<GeographyGeneratorResult> Generate(
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
        var stateTravelLinks = GenerateStateTravelLinks(
            new GenerateStateTravelLinksInput(context, states, countries.DominantRaceByCountryId)
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
            Locations = states.Locations,
            Props = states.Props,
            LocationConnectors = states.LocationConnectors,
            StateTravelLinks = stateTravelLinks,
            DominantRaceByCountryId = countries.DominantRaceByCountryId,
        };
    }

    private async Task<World> GenerateWorldEntity(
        GeographyGenerationContext context,
        CancellationToken cancellationToken
    )
    {
        var worldSchema = await client.GetValidatedJson<GeographyEntitySchema>(
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
            var schema = await client.GetValidatedJson<GeographyEntitySchema>(
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
        DistrictType.CityEntrance,
        DistrictType.Encampment,
        DistrictType.Governmental,
        DistrictType.HolySite,
    ];

    private static readonly DistrictType[] OptionalDistrictTypes = [DistrictType.Scientific];

    private const double OptionalDistrictChance = 0.55;
    private const double FocusedDistrictChance = 0.85;

    private static readonly Dictionary<CountryFocus, DistrictType> FocusDistrictTypes = new()
    {
        [CountryFocus.Scientific] = DistrictType.Scientific,
    };

    private static List<DistrictType> SelectDistrictTypes(CountryFocus focus)
    {
        var focusedDistrictType = FocusDistrictTypes.GetValueOrDefault(focus);
        var selected = new List<DistrictType>(RequiredDistrictTypes);
        foreach (var districtType in OptionalDistrictTypes)
        {
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
        var locations = new List<Location>();
        var props = new List<Prop>();
        var locationConnectors = new List<LocationConnector>();
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
                    .Select(_ => SelectDistrictTypes(country.Focus))
                    .ToList();

                for (var j = 0; j < cityStates.Count; j++)
                {
                    var mapState = cityStates[j];
                    var cityName = cityNames[j];
                    var districtTypes = cityDistrictTypes[j];
                    var state = new State
                    {
                        CountryId = country.Id,
                        Name = $"{cityName} Territory",
                        Description = $"The territory surrounding {cityName}.",
                        Width = CityTileSize,
                        Height = CityTileSize,
                        Center = mapState.Centroid,
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
                        Description = CreateCityDescription(
                            cityName,
                            country,
                            dominantRace,
                            mapState.IsCapital,
                            districtTypes
                        ),
                        IsCapital = mapState.IsCapital,
                        WorldId = world.Id,
                    };
                    states.Add(state);
                    cities.Add(city);
                    stateById[mapState.Id] = state;

                    var cityDistricts = new List<District>();
                    foreach (var districtType in districtTypes)
                    {
                        var districtResult = DistrictGenerator.Generate(
                            districtType,
                            city.Id,
                            state.Id,
                            world.Id
                        );
                        districts.Add(districtResult.District);
                        locations.Add(districtResult.Location);
                        cityDistricts.Add(districtResult.District);
                    }

                    var cityCenterDistrict = cityDistricts.First(d =>
                        d.DistrictType == DistrictType.CityCenter
                    );
                    locationConnectors.AddRange(
                        DistrictConnectorGenerator.Generate(
                            cityCenterDistrict,
                            cityDistricts
                                .Where(d => d.DistrictType != DistrictType.CityCenter)
                                .ToArray(),
                            world.Id
                        )
                    );
                }
            }
            for (var j = 0; j < nonCityStates.Count; j++)
            {
                var mapState = nonCityStates[j];
                var state = new State
                {
                    CountryId = country.Id,
                    Name = SettlementNameGenerator.GenerateWildernessName(
                        dominantRace,
                        GetConnectedStateNames(mapState.Id, context.Map.Roads, stateById)
                    ),
                    Description = "An untamed wilderness region.",
                    Width = CityTileSize,
                    Height = CityTileSize,
                    Center = mapState.Centroid,
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

        return new GeneratedStates(
            states,
            cities,
            districts,
            locations,
            props,
            locationConnectors,
            stateById
        );
    }

    private static HashSet<string> GetConnectedStateNames(
        Guid mapStateId,
        IReadOnlyCollection<MapRoad> roads,
        IReadOnlyDictionary<Guid, State> stateById
    ) =>
        roads
            .Where(road =>
                road.OriginStateId == mapStateId || road.DestinationStateId == mapStateId
            )
            .Select(road =>
                road.OriginStateId == mapStateId ? road.DestinationStateId : road.OriginStateId
            )
            .Select(stateById.GetValueOrDefault)
            .Where(state => state is not null)
            .Select(state => state!.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string CreateCityDescription(
        string cityName,
        Country country,
        CreatureType dominantRace,
        bool isCapital,
        IReadOnlyCollection<DistrictType> districtTypes
    )
    {
        var cityRole = isCapital ? "the capital" : "a city";
        var race = dominantRace.ToString().ToLowerInvariant();
        var districts = JoinPhrases(districtTypes.Select(GetDistrictLabel).ToArray());

        return $"{cityName} is {cityRole} of {country.Name}, a {race}-majority country shaped by {FocusDescriptions[country.Focus]}. Its districts include {districts}.";
    }

    private static string GetDistrictLabel(DistrictType districtType) =>
        districtType switch
        {
            DistrictType.Residential => "a residential district",
            DistrictType.Scientific => "a scientific district",
            DistrictType.CityCenter => "a city center",
            DistrictType.CityEntrance => "a city entrance",
            DistrictType.Governmental => "a governmental district",
            DistrictType.HolySite => "a holy site",
            DistrictType.Encampment => "an encampment",
            _ => throw new ArgumentOutOfRangeException(nameof(districtType)),
        };

    private static string JoinPhrases(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}",
        };

    private static List<StateTravelLink> GenerateStateTravelLinks(
        GenerateStateTravelLinksInput input
    )
    {
        var context = input.Context;
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
                return new StateTravelLink(
                    SettlementNameGenerator.GenerateRoadName(dominantRace, usedRoadNames),
                    originState.Id,
                    destState.Id,
                    distance,
                    Math.Max(1, (int)(distance / 50)),
                    (float)Random.Shared.NextDouble() * 0.5f
                );
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
    List<Location> Locations,
    List<Prop> Props,
    List<LocationConnector> LocationConnectors,
    Dictionary<Guid, State> StateById
);

internal record GenerateStatesInput(
    GeographyGenerationContext Context,
    World World,
    GeneratedCountries Countries
);

internal record GenerateStateTravelLinksInput(
    GeographyGenerationContext Context,
    GeneratedStates States,
    IReadOnlyDictionary<Guid, CreatureType> DominantRaceByCountryId
);

internal record StateTravelLink(
    string Name,
    Guid OriginStateId,
    Guid DestinationStateId,
    float Distance,
    int TravelTimeHours,
    float DangerLevel
);

internal class GeographyEntitySchema
{
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
}
