using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class GeographyGeneratorTests
{
    [Fact]
    public async Task Generate_DescribesCitiesFromTheirGeneratedFacts()
    {
        // Arrange
        var generator = new GeographyGenerator(
            new FakeChatClient(),
            NullLogger<GeographyGenerator>.Instance
        );
        var input = new GeographyGeneratorInput
        {
            Description = "A test world.",
            MinCityStates = 1,
            MaxCityStates = 1,
            MinRuralStates = 5,
            MaxRuralStates = 5,
        };

        // Act
        var result = await generator.Generate(input, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(
            result.Cities,
            city =>
            {
                var country = Assert.Single(
                    result.Countries,
                    country => country.Id == city.CountryId
                );
                var cityRole = city.IsCapital ? "the capital" : "a city";

                Assert.Contains(
                    $"{city.Name} is {cityRole} of {country.Name}",
                    city.Description,
                    StringComparison.Ordinal
                );
                Assert.Contains(
                    $"{country.DominantRace.ToString().ToLowerInvariant()}-majority",
                    city.Description,
                    StringComparison.Ordinal
                );
                Assert.Contains(
                    GetFocusDescription(country.Focus),
                    city.Description,
                    StringComparison.Ordinal
                );
                Assert.Contains(
                    "a residential district",
                    city.Description,
                    StringComparison.Ordinal
                );
                Assert.Contains("a city center", city.Description, StringComparison.Ordinal);
                Assert.Contains("a city entrance", city.Description, StringComparison.Ordinal);
                Assert.Contains(
                    result.Districts,
                    district =>
                        district.CityId == city.Id
                        && district.DistrictType == DistrictType.CityEntrance
                );

                var cityEntrance = result.Districts.Single(district =>
                    district.CityId == city.Id && district.DistrictType == DistrictType.CityEntrance
                );
                var cityCenter = result.Districts.Single(district =>
                    district.CityId == city.Id && district.DistrictType == DistrictType.CityCenter
                );
                Assert.Contains(
                    result.LocationConnectors,
                    connector =>
                        connector.OriginLocationId == cityEntrance.LocationId
                        && connector.DestinationLocationId == cityCenter.LocationId
                );
                Assert.Contains(
                    result.LocationConnectors,
                    connector =>
                        connector.OriginLocationId == cityCenter.LocationId
                        && connector.DestinationLocationId == cityEntrance.LocationId
                );
            }
        );
        Assert.All(
            result.States.Where(state => result.Cities.All(city => city.StateId != state.Id)),
            state => Assert.False(state.Name.StartsWith("Wilderness ", StringComparison.Ordinal))
        );
    }

    [Fact]
    public async Task Generate_SetsEachStatesCenterToItsBoundarysPolygonCentroid()
    {
        // Arrange
        var generator = new GeographyGenerator(
            new FakeChatClient(),
            NullLogger<GeographyGenerator>.Instance
        );
        var input = new GeographyGeneratorInput
        {
            Description = "A test world.",
            MinCityStates = 1,
            MaxCityStates = 1,
            MinRuralStates = 5,
            MaxRuralStates = 5,
        };

        // Act
        var result = await generator.Generate(input, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(
            result.States,
            state =>
            {
                // Center is stored as a truncated integer, computed from the Voronoi cell's own
                // (untruncated) centroid — while this reference value is computed from Boundary's
                // own already-truncated points. Truncating at two different stages of the same
                // computation can legitimately land a fraction of a unit apart, so this checks
                // Center is close to the boundary's centroid, not bit-for-bit equal to it.
                var expectedCenter = PolygonCentroid(state.Boundary);
                Assert.True(
                    Math.Abs(expectedCenter.X - state.Center.X) < 1.5,
                    $"State '{state.Name}': Center.X {state.Center.X} is too far from the boundary's own centroid {expectedCenter.X}."
                );
                Assert.True(
                    Math.Abs(expectedCenter.Y - state.Center.Y) < 1.5,
                    $"State '{state.Name}': Center.Y {state.Center.Y} is too far from the boundary's own centroid {expectedCenter.Y}."
                );
            }
        );
    }

    // Independently re-derives the expected centroid via the standard shoelace-formula
    // computation, rather than calling the generator's own private helper, so this actually
    // verifies the contract rather than restating it.
    private static Point PolygonCentroid(Polygon boundary)
    {
        var points = boundary.Points;
        var area = 0.0;
        var centroidX = 0.0;
        var centroidY = 0.0;

        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            var cross = current.X * next.Y - next.X * current.Y;
            area += cross;
            centroidX += (current.X + next.X) * cross;
            centroidY += (current.Y + next.Y) * cross;
        }

        area *= 0.5;
        return new Point(centroidX / (6 * area), centroidY / (6 * area));
    }

    private static string GetFocusDescription(CountryFocus focus) =>
        focus switch
        {
            CountryFocus.Scientific => "scientific and magical pursuits",
            CountryFocus.Political => "political power and bureaucracy",
            CountryFocus.Religious => "religious devotion",
            CountryFocus.Militaristic => "martial strength and conquest",
        };
}
