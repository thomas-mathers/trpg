using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Signs.Responses;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class SignEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();

    private TestApiClient _client = null!;
    private Guid _worldId;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateApiClient();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _worldId = world.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetSignText_ReturnsNotFound_WhenTheSignDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync(
            "GetSignText",
            routeValues: new { signId = Guid.NewGuid() },
            query: new Dictionary<string, object?> { ["worldId"] = _worldId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSignText_ReturnsTheStoredDescription_ForAPlainSign()
    {
        // Arrange
        var sign = Builders.MakeSign(_worldId, _locationA, "Beware of the dog.");
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Props.Add(sign);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var result = await _client.ReadFromJsonAsync<SignTextResponse>(
            "GetSignText",
            routeValues: new { signId = sign.Id },
            query: new Dictionary<string, object?> { ["worldId"] = _worldId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("Beware of the dog.", result!.Text);
    }

    [Fact]
    public async Task GetSignText_ReturnsLiveArrivalText_ForACaravanScheduleSign()
    {
        // Arrange — 2 stops, 10 units apart each way at speed 5 = 2 leg hours; with a 1-hour
        // linger the total cycle is 2 * (1 + 2) = 6 hours, and stop A's window is [0, 1).
        var route = Builders.MakeCaravanRoute(_worldId, lingerHours: 1);
        var stopA = Builders.MakeCaravanRouteStop(route.Id, 0, _locationA, distanceToNextStop: 10);
        var stopB = Builders.MakeCaravanRouteStop(route.Id, 1, _locationB, distanceToNextStop: 10);
        var fare = Builders.MakeCaravanFare(route.Id, _worldId);
        var caravan = Builders.MakeCaravan(route.Id, _worldId, phaseOffsetHours: 0);
        var sign = new CaravanScheduleSign
        {
            WorldId = _worldId,
            LocationId = _locationA,
            Name = "Caravan Schedule",
            Description = "A wooden signpost listing caravan arrival times.",
        };
        var session = Builders.MakeGameSession(
            _worldId,
            Guid.NewGuid(),
            playtime: GameClock.RealTimePerInGameHour * 2
        );

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Routes.Add(route);
            context.RouteSteps.AddRange(stopA, stopB);
            context.CaravanFares.Add(fare);
            context.RouteTravelers.Add(caravan);
            context.Props.Add(sign);
            context.GameSessions.Add(session);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var result = await _client.ReadFromJsonAsync<SignTextResponse>(
            "GetSignText",
            routeValues: new { signId = sign.Id },
            query: new Dictionary<string, object?> { ["worldId"] = _worldId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert — stop A's window [0, 1) closed 1 hour ago at playtime 2 in-game hours, so the
        // live text should report a future arrival rather than the sign's stored placeholder.
        Assert.StartsWith("Caravan schedule:", result!.Text);
        Assert.Contains("Clockwise: next arrival", result.Text);
        Assert.DoesNotContain("A wooden signpost", result.Text);
    }
}
