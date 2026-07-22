using System.Net;
using System.Net.Http.Json;
using TRPG.Contracts;
using TRPG.Contracts.Creatures.Responses;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class CreatureGenerationEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = fixture.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetOptions_ReturnsPointsPerLevelAndBaseAttributes()
    {
        // Act
        var response = await _client.GetAsync(
            new Uri("/creature-generation/options", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert — matches CreatureGeneratorOptions' code defaults (no test-specific override)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreatureGenerationOptionsResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(5, result.PointsPerLevel);
        Assert.Equal(1, result.BaseAttributes.Strength);
        Assert.Equal(1, result.BaseAttributes.Defense);
        Assert.Equal(1, result.BaseAttributes.Dexterity);
        Assert.Equal(1, result.BaseAttributes.Endurance);
        Assert.Equal(1, result.BaseAttributes.Stamina);
        Assert.Equal(1, result.BaseAttributes.Mana);
        Assert.Equal(1, result.BaseAttributes.Intelligence);
    }
}
