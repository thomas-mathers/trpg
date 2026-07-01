using System.Collections.ObjectModel;
using SharpVoronoiLib;
using TRPG.Algorithms;
using TRPG.Models;

namespace TRPG.Generators;

internal class MapCountry {
    public required Polygon Boundary { get; init; }
    public Guid Id { get; } = Guid.NewGuid();
}

internal class MapRegion {
    public required Polygon Boundary { get; init; }
    public required Point Center { get; init; }
    public required Guid CountryId { get; init; }
    public Guid Id { get; } = Guid.NewGuid();
    public bool IsCapital { get; init; }
    public RegionType RegionType { get; init; }
}

internal class MapRoad {
    public required Guid DestinationRegionId { get; init; }
    public Guid Id { get; } = Guid.NewGuid();
    public required Guid OriginRegionId { get; init; }
}

internal class MapGeneratorResult {
    public required IReadOnlyList<MapCountry> Countries { get; init; }
    public required IReadOnlyList<MapRegion> Regions { get; init; }
    public required IReadOnlyList<MapRoad> Roads { get; init; }
}

internal static class MapGenerator {
    public static MapGeneratorResult Generate(int worldWidth, int worldHeight, int numUrbanRegions,
        int numRuralRegions, int numberOfCountries) {
        var plane = new VoronoiPlane(0, 0, worldWidth, worldHeight);

        var sites = plane.GenerateRandomSites(numUrbanRegions + numRuralRegions);

        plane.Tessellate();

        var (siteToCountryIndex, capitalSites) = AssignSitesToCountries(sites, numberOfCountries);

        var countries = Enumerable.Range(0, numberOfCountries)
            .Select(i => new MapCountry { Boundary = GetCountryBoundary(i, siteToCountryIndex) })
            .ToArray();

        var ruralSites = GetRuralSites(sites, capitalSites, numRuralRegions);

        var regionBySite = sites.ToDictionary(
            k => k,
            site => new MapRegion {
                CountryId = countries[siteToCountryIndex[site]].Id,
                Center = new Point((int) site.X, (int) site.Y),
                Boundary = new Polygon {
                    Points = [..site.ClockwiseEdgesWound.Select(e => new Point((int) e.Start.X, (int) e.Start.Y))]
                },
                IsCapital = capitalSites.Contains(site),
                RegionType = ruralSites.Contains(site) ? RegionType.Rural : RegionType.Urban
            });

        var roads = GetRoads(regionBySite);

        return new MapGeneratorResult {
            Countries = countries,
            Regions = regionBySite.Values.ToArray(),
            Roads = roads
        };
    }

    private static (Dictionary<VoronoiSite, int> SiteToCountry, HashSet<VoronoiSite> CapitalSites)
        AssignSitesToCountries(List<VoronoiSite> sites, int numCountries) {
        var frontier = new Queue<(int CountryIndex, VoronoiSite Site)>();
        var siteToCountry = new Dictionary<VoronoiSite, int>();

        var capitalSites = sites.Shuffle().Take(numCountries).ToArray();

        for (var i = 0; i < capitalSites.Length; i++) {
            frontier.Enqueue((i, capitalSites[i]));
        }

        while (frontier.Count > 0) {
            var (countryIndex, site) = frontier.Dequeue();

            if (!siteToCountry.TryAdd(site, countryIndex)) {
                continue;
            }

            foreach (var neighbour in site.Neighbours.OrderBy(n => DistanceTo(site, n))) {
                frontier.Enqueue((countryIndex, neighbour));
            }
        }

        return (siteToCountry, [..capitalSites]);
    }

    private static HashSet<VoronoiSite> GetRuralSites(
        List<VoronoiSite> sites,
        HashSet<VoronoiSite> capitalSites,
        int numRuralRegions) {
        var nonCapitals = sites.Where(s => !capitalSites.Contains(s)).ToList();
        return [..nonCapitals.Shuffle().Take(numRuralRegions)];
    }

    private static Polygon GetCountryBoundary(int countryIndex, Dictionary<VoronoiSite, int> siteToCountryIndex) {
        var countrySites = siteToCountryIndex
            .Where(kvp => kvp.Value == countryIndex)
            .Select(kvp => kvp.Key)
            .ToArray();

        if (countrySites.Length == 0) {
            throw new InvalidOperationException("Country has no regions");
        }

        var boundaryEdges = countrySites
            .SelectMany(c => c.ClockwiseEdges)
            .Where(e => e.IsBoundaryEdge(siteToCountryIndex))
            .Distinct()
            .ToList();

        if (boundaryEdges.Count == 0) {
            throw new InvalidOperationException("Country does not have a closed boundary");
        }

        var edgesByVertex = new Dictionary<VoronoiPoint, List<VoronoiEdge>>();
        foreach (var edge in boundaryEdges) {
            if (!edgesByVertex.TryGetValue(edge.Start, out var startList)) {
                edgesByVertex[edge.Start] = startList = [];
            }

            startList.Add(edge);

            if (!edgesByVertex.TryGetValue(edge.End, out var endList)) {
                edgesByVertex[edge.End] = endList = [];
            }

            endList.Add(edge);
        }

        var points = new List<Point>();
        var exploredEdges = new HashSet<VoronoiEdge>();

        var currentEdge = boundaryEdges[0];
        var entryVertex = currentEdge.Start;

        while (currentEdge is not null) {
            exploredEdges.Add(currentEdge);

            var exitVertex = currentEdge.Start == entryVertex ? currentEdge.End : currentEdge.Start;
            points.Add(new Point((int) exitVertex.X, (int) exitVertex.Y));

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

    private static bool IsBoundaryEdge(this VoronoiEdge edge, Dictionary<VoronoiSite, int> siteToCountryIndex) {
        var left = edge.Left;
        var right = edge.Right;

        if (left == null && right == null) {
            return false;
        }

        if (left == null || right == null) {
            return true;
        }

        return siteToCountryIndex[left] != siteToCountryIndex[right];
    }

    private static MapRoad[] GetRoads(Dictionary<VoronoiSite, MapRegion> regionBySite) {
        var edges = Graphs.MinimumSpanningTree(
            regionBySite.Keys.First(),
            site => site.Neighbours,
            DistanceTo);

        return edges
            .Select(edge => new MapRoad {
                OriginRegionId = regionBySite[edge.From].Id,
                DestinationRegionId = regionBySite[edge.To].Id
            })
            .ToArray();
    }

    private static double DistanceTo(VoronoiSite a, VoronoiSite b) {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
