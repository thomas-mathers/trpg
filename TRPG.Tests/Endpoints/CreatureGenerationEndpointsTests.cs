using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
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
        // Arrange — compare against whatever the app itself has configured, not a hardcoded
        // literal, so tuning CreatureGenerator's appsettings.json values doesn't break this test
        await using var scope = fixture.CreateScope();
        var options = scope
            .ServiceProvider.GetRequiredService<IOptionsSnapshot<CreatureGeneratorOptions>>()
            .Value;

        // Act
        var response = await _client.GetAsync(
            new Uri("/creature-generation/options", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreatureGenerationOptionsResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(options.PointsPerLevel, result.PointsPerLevel);
        Assert.Equal(options.BaseAttributes.Strength, result.BaseAttributes.Strength);
        Assert.Equal(options.BaseAttributes.Defense, result.BaseAttributes.Defense);
        Assert.Equal(options.BaseAttributes.Dexterity, result.BaseAttributes.Dexterity);
        Assert.Equal(options.BaseAttributes.Endurance, result.BaseAttributes.Endurance);
        Assert.Equal(options.BaseAttributes.Stamina, result.BaseAttributes.Stamina);
        Assert.Equal(options.BaseAttributes.Mana, result.BaseAttributes.Mana);
        Assert.Equal(options.BaseAttributes.Intelligence, result.BaseAttributes.Intelligence);
    }
}
