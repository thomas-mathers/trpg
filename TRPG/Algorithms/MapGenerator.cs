using System.Collections.ObjectModel;
using SharpVoronoiLib;
using TRPG.Models;

namespace TRPG.Algorithms;

internal class MapCountry {
    public Guid Id { get; } = Guid.NewGuid();
    public required Polygon Boundary { get; init; }
}

internal class MapCity {
    public Guid Id { get; } = Guid.NewGuid();
    public required Guid CountryId { get; init; }
    public required Point Center { get; init; }
    public required Polygon Boundary { get; init; }
}

internal class MapRoad {
    public Guid Id { get; } = Guid.NewGuid();
    public required Guid OriginCityId { get; init; }
    public required Guid DestinationCityId { get; init; }
}

internal class MapGeneratorResult {
    public required IReadOnlyList<MapCountry> Countries { get; init; }
    public required IReadOnlyList<MapCity> Cities { get; init; }
    public required IReadOnlyList<MapRoad> Roads { get; init; }
}

internal static class MapGenerator {
    public static MapGeneratorResult Generate(int worldWidth, int worldHeight, int numberOfCities, int numberOfCountries) {
        var plane = new VoronoiPlane(0, 0, worldWidth, worldHeight);

        var citySites = plane.GenerateRandomSites(numberOfCities);

        plane.Tessellate();

        var cityToCountryIndex = AssignCitiesToCountries(citySites, numberOfCountries);

        var countries = Enumerable.Range(0, numberOfCountries)
            .Select(i => new MapCountry { Boundary = GetCountryBoundary(i, cityToCountryIndex) })
            .ToArray();

        var cityBySite = citySites.ToDictionary(
            k => k,
            site => new MapCity {
                CountryId = countries[cityToCountryIndex[site]].Id,
                Center = new Point((int)site.X, (int)site.Y),
                Boundary = new Polygon {
                    Points = [..site.ClockwiseEdgesWound.Select(e => new Point((int)e.Start.X, (int)e.Start.Y))]
                }
            });

        var roads = GetRoads(cityBySite);

        return new MapGeneratorResult {
            Countries = countries,
            Cities = cityBySite.Values.ToArray(),
            Roads = roads
        };
    }

    private static Dictionary<VoronoiSite, int> AssignCitiesToCountries(List<VoronoiSite> cities, int numCountries) {
        var frontier = new Queue<(int CountryIndex, VoronoiSite CitySite)>();
        var cityToCountry = new Dictionary<VoronoiSite, int>();

        var capitalCities = cities.Shuffle().Take(numCountries).ToArray();

        for (var i = 0; i < capitalCities.Length; i++) {
            frontier.Enqueue((i, capitalCities[i]));
        }

        while (frontier.Count > 0) {
            var (countryIndex, city) = frontier.Dequeue();

            if (!cityToCountry.TryAdd(city, countryIndex)) continue;

            foreach (var neighbour in city.Neighbours.OrderBy(n => DistanceTo(city, n))) {
                frontier.Enqueue((countryIndex, neighbour));
            }
        }

        return cityToCountry;
    }

    private static Polygon GetCountryBoundary(int countryIndex, Dictionary<VoronoiSite, int> cityToCountryIndex) {
        var countryCities = cityToCountryIndex
            .Where(kvp => kvp.Value == countryIndex)
            .Select(kvp => kvp.Key)
            .ToArray();

        if (countryCities.Length == 0) {
            throw new InvalidOperationException("Country has no cities");
        }

        var boundaryEdges = countryCities
            .SelectMany(c => c.ClockwiseEdges)
            .Where(e => e.IsBoundaryEdge(cityToCountryIndex))
            .Distinct()
            .ToList();

        if (boundaryEdges.Count == 0) {
            throw new InvalidOperationException("Country does not have a closed boundary");
        }

        var edgesByVertex = new Dictionary<VoronoiPoint, List<VoronoiEdge>>();
        foreach (var edge in boundaryEdges) {
            if (!edgesByVertex.TryGetValue(edge.Start, out var startList))
                edgesByVertex[edge.Start] = startList = [];
            startList.Add(edge);

            if (!edgesByVertex.TryGetValue(edge.End, out var endList))
                edgesByVertex[edge.End] = endList = [];
            endList.Add(edge);
        }

        var points = new List<Point>();
        var exploredEdges = new HashSet<VoronoiEdge>();

        var currentEdge = boundaryEdges[0];
        var entryVertex = currentEdge.Start;

        while (currentEdge is not null) {
            exploredEdges.Add(currentEdge);

            var exitVertex = currentEdge.Start == entryVertex ? currentEdge.End : currentEdge.Start;
            points.Add(new Point((int)exitVertex.X, (int)exitVertex.Y));

            var nextEdge = edgesByVertex.TryGetValue(exitVertex, out var candidates)
                ? candidates.FirstOrDefault(e => !exploredEdges.Contains(e))
                : null;

            entryVertex = exitVertex;
            currentEdge = nextEdge;
        }

        if (points.Count < 3) {
            throw new InvalidOperationException("Country boundary walk did not form a closed polygon");
        }

        return new Polygon { Points = new Collection<Point>(points) };
    }

    private static bool IsBoundaryEdge(this VoronoiEdge edge, Dictionary<VoronoiSite, int> cityToCountryIndex) {
        var left = edge.Left;
        var right = edge.Right;

        if (left == null && right == null) return false;
        if (left == null || right == null) return true;

        return cityToCountryIndex[left] != cityToCountryIndex[right];
    }

    private static MapRoad[] GetRoads(Dictionary<VoronoiSite, MapCity> cityBySite) {
        var edges = Graphs.MinimumSpanningTree(
            cityBySite.Keys.First(),
            city => city.Neighbours,
            DistanceTo);

        return edges
            .Select(edge => new MapRoad {
                OriginCityId = cityBySite[edge.From].Id,
                DestinationCityId = cityBySite[edge.To].Id
            })
            .ToArray();
    }

    private static double DistanceTo(VoronoiSite a, VoronoiSite b) {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
