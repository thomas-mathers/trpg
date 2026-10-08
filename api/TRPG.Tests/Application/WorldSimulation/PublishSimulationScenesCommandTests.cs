using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Scenes.Events;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class PublishSimulationScenesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<PublishSimulationScenesCommand> _handler = null!;
    private TestGameClientEventSink _sink = null!;
    private World _world = null!;
    private Location _playerLocation = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<PublishSimulationScenesCommand>
        >();
        _sink = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        _world = Builders.MakeWorld();
        var country = Builders.MakeCountry(_world.Id);
        var state = Builders.MakeState(country.Id);
        _playerLocation = Builders.MakeLocation(_world.Id, state.Id, kind: LocationKind.Wilderness);
        var player = Builders.MakeCreature(_world.Id, locationId: _playerLocation.Id);
        _world.PlayerId = player.Id;
        _context.Worlds.Add(_world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.Add(_playerLocation);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PublishesTheScene_WhenAChangedLocationIsThePlayers()
    {
        // Arrange
        var command = MakeCommand(_playerLocation.Id, Guid.NewGuid());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sink.EnqueuedEvents.OfType<SceneUpdatedEvent>());
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenNoChangedLocationIsThePlayers()
    {
        // Arrange
        var command = MakeCommand(Guid.NewGuid());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_sink.EnqueuedEvents);
    }

    private PublishSimulationScenesCommand MakeCommand(params Guid[] changedLocationIds) =>
        new()
        {
            WorldId = _world.Id,
            GameTime = Now,
            ChangedCreatureIdsByLocationId = changedLocationIds.ToDictionary(
                locationId => locationId,
                _ => (IReadOnlySet<Guid>)new HashSet<Guid> { Guid.NewGuid() }
            ),
        };
}
