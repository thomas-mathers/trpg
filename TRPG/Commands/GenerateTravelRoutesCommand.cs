using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateTravelRoutesCommand {
    public required IReadOnlyList<City> Cities { get; init; }
    public int Count { get; init; }
    public required string Description { get; init; }
}

internal class GenerateTravelRoutesCommandHandler(AiClient client, ILogger<GenerateTravelRoutesCommandHandler> logger) {
    public async Task<IReadOnlyList<TravelRoute>> Handle(
        GenerateTravelRoutesCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        var cityNames = string.Join(", ", command.Cities.Select(c => c.Name));

        var exampleOrigin = command.Cities[0].Name;
        var exampleDestination = command.Cities[1].Name;
        var example =
            $$"""{"Name":"The King's Road","OriginCityName":"{{exampleOrigin}}","DestinationCityName":"{{exampleDestination}}","Distance":120.0,"DangerLevel":0.2,"TravelTime":8}""";
        
        var chat = client.CreateChat<GenerateTravelRoutesCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             When asked to generate a travel route, respond with a single JSON object with these fields:
             - Name: a descriptive route name (e.g. "The King's Road", "Smuggler's Pass")
             - OriginCityName and DestinationCityName: must exactly match names from this list: {cityNames}
             - Distance: in kilometres (10-500)
             - DangerLevel: 0.0 (safe) to 1.0 (extremely dangerous)
             - TravelTime: in hours (1-72)
             Do not repeat the same pair of cities. Each city should appear in at least one route. Do not use markdown.
             Example: {example}
             """);

        var routes = new List<TravelRoute>();
        var usedPairs = new HashSet<(Guid, Guid)>();
        for (var i = 0; i < command.Count; i++) {
            var exclusions = usedPairs.Count > 0
                ? " Do not use these already-taken pairs: " + string.Join(", ", usedPairs.Select(p =>
                    $"{command.Cities.First(c => c.Id == p.Item1).Name}↔{command.Cities.First(c => c.Id == p.Item2).Name}")) + "."
                : "";
            var schema = await chat.GetJson<TravelRouteSchema>(
                $"Generate travel route {i + 1} of {command.Count}.{exclusions}",
                cancellationToken);
            var route = MapRoute(command.Cities, schema, usedPairs);
            if (route is not null) {
                usedPairs.Add((route.OriginCityId, route.DestinationCityId));
                routes.Add(route);
            }
        }

        logger.LogDebug("GenerateTravelRoutes completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        return routes;
    }

    private TravelRoute? MapRoute(IReadOnlyList<City> cities, TravelRouteSchema schema, HashSet<(Guid, Guid)> usedPairs) {
        var origin =
            cities.FirstOrDefault(c => c.Name.Equals(schema.OriginCityName, StringComparison.OrdinalIgnoreCase));
        var destination = cities.FirstOrDefault(c =>
            c.Name.Equals(schema.DestinationCityName, StringComparison.OrdinalIgnoreCase));
        if (origin is null || destination is null) {
            logger.LogWarning("Dropped route '{Route}': city not found ('{Origin}' -> '{Destination}')", schema.Name, schema.OriginCityName, schema.DestinationCityName);
            return null;
        }
        if (origin.Id == destination.Id) {
            logger.LogWarning("Dropped route '{Route}': origin and destination are the same city '{City}'", schema.Name, schema.OriginCityName);
            return null;
        }
        if (usedPairs.Contains((origin.Id, destination.Id)) || usedPairs.Contains((destination.Id, origin.Id))) {
            logger.LogWarning("Dropped route '{Route}': duplicate pair '{Origin}' <-> '{Destination}'", schema.Name, schema.OriginCityName, schema.DestinationCityName);
            return null;
        }

        return new TravelRoute {
            Name = schema.Name,
            OriginCityId = origin.Id,
            DestinationCityId = destination.Id,
            Distance = schema.Distance,
            DangerLevel = schema.DangerLevel,
            TravelTime = schema.TravelTime
        };
    }
}