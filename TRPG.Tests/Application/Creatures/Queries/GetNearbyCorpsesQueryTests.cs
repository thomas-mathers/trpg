using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetNearbyCorpsesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    // Instance (not static) fields: each [Fact] gets its own xUnit class instance, so fresh
    // Guids here keep every test's room/district isolated from every other test's seeded
    // creatures — there's no transaction rollback between tests on the shared Postgres container.
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _otherRoomId = Guid.NewGuid();
    private readonly Guid _districtId = Guid.NewGuid();
    private readonly Guid _otherDistrictId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNearbyCorpsesQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetNearbyCorpsesQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyDeadCreatures_InThePlayersRoom()
    {
        // Arrange
        var player = Builders.MakeCreature(_worldId, stateId: _stateId, roomId: _roomId);
        var corpse = Builders.MakeCreature(
            _worldId,
            stateId: _stateId,
            roomId: _roomId,
            state: CreatureState.Dead,
            name: "Corpse"
        );
        var livingCreature = Builders.MakeCreature(
            _worldId,
            stateId: _stateId,
            roomId: _roomId,
            name: "Living"
        );
        _context.Creatures.AddRange(player, corpse, livingCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var found = Assert.Single(result);
        Assert.Equal(corpse.Id, found.Id);
    }

    [Fact]
    public async Task Handle_ExcludesTheQueryingPlayer()
    {
        // Arrange — the player themselves is dead too, but must never appear in their own list
        var player = Builders.MakeCreature(
            _worldId,
            stateId: _stateId,
            roomId: _roomId,
            state: CreatureState.Dead
        );
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ExcludesCorpsesInADifferentRoom()
    {
        // Arrange
        var player = Builders.MakeCreature(_worldId, stateId: _stateId, roomId: _roomId);
        var farCorpse = Builders.MakeCreature(
            _worldId,
            stateId: _stateId,
            roomId: _otherRoomId,
            state: CreatureState.Dead
        );
        _context.Creatures.AddRange(player, farCorpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ScopesByDistrict_WhenOutdoors()
    {
        // Arrange
        var player = Builders.MakeCreature(_worldId, stateId: _stateId, districtId: _districtId);
        var nearbyCorpse = Builders.MakeCreature(
            _worldId,
            stateId: _stateId,
            districtId: _districtId,
            state: CreatureState.Dead
        );
        var farCorpse = Builders.MakeCreature(
            _worldId,
            stateId: _stateId,
            districtId: _otherDistrictId,
            state: CreatureState.Dead
        );
        _context.Creatures.AddRange(player, nearbyCorpse, farCorpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var found = Assert.Single(result);
        Assert.Equal(nearbyCorpse.Id, found.Id);
    }
}
