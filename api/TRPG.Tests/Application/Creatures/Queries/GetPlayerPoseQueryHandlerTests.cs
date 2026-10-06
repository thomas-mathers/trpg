using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

public sealed class GetPlayerPoseQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetPlayerPoseQueryHandler _handler = null!;
    private PlayerPoseStore _poseStore = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetPlayerPoseQueryHandler>();
        _poseStore = _serviceProvider.GetRequiredService<PlayerPoseStore>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheLivePose_WhenItIsForTheCurrentLocation()
    {
        // Arrange
        var player = await SeedPlayer(Guid.NewGuid(), x: 1, y: 2, angle: 3);
        _poseStore.Set(
            player.Id,
            new PlayerPose(player.LocationId, 10, 20, 0.5, DateTimeOffset.UnixEpoch, IsDirty: true)
        );

        // Act
        var pose = await _handler.Handle(
            new GetPlayerPoseQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Placement(10, 20, 0.5), pose);
    }

    [Fact]
    public async Task Handle_ReturnsTheStoredPose_WhenTheLivePoseIsForAnotherLocation()
    {
        // Arrange
        var player = await SeedPlayer(Guid.NewGuid(), x: 1, y: 2, angle: 3);
        _poseStore.Set(
            player.Id,
            new PlayerPose(Guid.NewGuid(), 10, 20, 0.5, DateTimeOffset.UnixEpoch, IsDirty: true)
        );

        // Act
        var pose = await _handler.Handle(
            new GetPlayerPoseQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Placement(1, 2, 3), pose);
    }

    [Fact]
    public async Task Handle_ReturnsTheStoredPose_WhenNoLivePoseExists()
    {
        // Arrange
        var player = await SeedPlayer(Guid.NewGuid(), x: 1, y: 2, angle: 3);

        // Act
        var pose = await _handler.Handle(
            new GetPlayerPoseQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Placement(1, 2, 3), pose);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenThePlayerDoesNotExist()
    {
        // Act
        var pose = await _handler.Handle(
            new GetPlayerPoseQuery { PlayerId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(pose);
    }

    private async Task<Creature> SeedPlayer(Guid locationId, double x, double y, double angle)
    {
        var player = Builders.MakeCreature(
            WorldId,
            locationId: locationId,
            x: x,
            y: y,
            angle: angle
        );
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return player;
    }
}
