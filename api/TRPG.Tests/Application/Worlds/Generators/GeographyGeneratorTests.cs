using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

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
            }
        );
    }

    private static string GetFocusDescription(CountryFocus focus) =>
        focus switch
        {
            CountryFocus.Scientific => "scientific and magical pursuits",
            CountryFocus.Political => "political power and bureaucracy",
            CountryFocus.Religious => "religious devotion",
            CountryFocus.Militaristic => "martial strength and conquest",
            _ => throw new ArgumentOutOfRangeException(nameof(focus)),
        };
}
