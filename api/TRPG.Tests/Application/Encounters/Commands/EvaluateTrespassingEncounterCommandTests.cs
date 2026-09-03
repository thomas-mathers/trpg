using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class EvaluateTrespassingEncounterCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    // Instance field, not the usual shared static: SetTrespassingBuildingCommand/
    // GetTrespassingBuildingIdQuery treat GameSession as one-per-world, so a WorldId shared across
    // every [Fact] in this class would leave multiple GameSession rows under the same WorldId.
    private readonly Guid _worldId = Guid.NewGuid();

    private readonly TestChanceRoller _chanceRoller = new() { Result = true };
    private readonly Guid _buildingId = Guid.NewGuid();
    private readonly Guid _roomLocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateTrespassingEncounterCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(_worldId, locationId: _roomLocationId);
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<LockpickingOptions>>(
                new TestOptionsMonitor<LockpickingOptions>(new LockpickingOptions())
            )
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<EvaluateTrespassingEncounterCommandHandler>();

        var building = Builders.MakeBuilding(
            worldId: _worldId,
            buildingType: BuildingType.House,
            id: _buildingId
        );
        var room = Builders.MakeRoom(_buildingId, worldId: _worldId, locationId: _roomLocationId);
        var location = Builders.MakeLocation(
            worldId: _worldId,
            id: _roomLocationId,
            roomId: room.Id
        );

        _context.Creatures.Add(_player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(_player.Id, Skill.Sneak, level: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFlaggedAsTrespassing()
    {
        // Arrange — an ordinary visit (e.g. a shop through an unlocked door): no lock was ever
        // picked, so no GameSession row flags the player as trespassing here, even with an occupant present.
        var occupant = Builders.MakeCreature(_worldId, locationId: _roomLocationId);
        _context.Creatures.Add(occupant);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = _worldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoOneIsPresent()
    {
        // Arrange
        _context.GameSessions.Add(
            Builders.MakeGameSession(_worldId, _player.Id, trespassingBuildingId: _buildingId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = _worldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsHostileEncounterWithTheOccupant_WhenTrespassingAndDetected()
    {
        // Arrange
        var occupant = Builders.MakeCreature(_worldId, locationId: _roomLocationId);
        var faction = Builders.MakeFaction(worldId: _worldId, isCityFaction: true);
        _context.Creatures.Add(occupant);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(_worldId, faction.Id, occupant.Id));
        _context.GameSessions.Add(
            Builders.MakeGameSession(_worldId, _player.Id, trespassingBuildingId: _buildingId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = _worldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(faction.Id, result.FactionId);
        var member = Assert.Single(result.Members);
        Assert.Equal(occupant.Id, member.Id);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotDetected()
    {
        // Arrange
        var occupant = Builders.MakeCreature(_worldId, locationId: _roomLocationId);
        _context.Creatures.Add(occupant);
        _context.GameSessions.Add(
            Builders.MakeGameSession(_worldId, _player.Id, trespassingBuildingId: _buildingId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = _worldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ClearsTheFlag_WhenPlayerHasLeftTheFlaggedBuilding()
    {
        // Arrange — the session still says the player broke into some other building, but they're
        // now standing in a different one (or outside), so the flag is stale and should be cleared.
        var otherBuildingId = Guid.NewGuid();
        _context.GameSessions.Add(
            Builders.MakeGameSession(_worldId, _player.Id, trespassingBuildingId: otherBuildingId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = _worldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
        await using var verifyContext = db.CreateContext();
        var session = await verifyContext.GameSessions.SingleAsync(
            s => s.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Null(session.TrespassingBuildingId);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; } = true;

        public bool Roll(float chance) => Result;
    }
}
