using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Encounters.Events;
using TRPG.Application.GameTurns;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Extensions;
using TRPG.RoomBookings.Tools;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Tools;

public sealed class ReturnRoomKeyToolTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ReturnRoomKeyTool _tool = null!;
    private RoomKeyFixture _roomKey = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddGameTool<ReturnRoomKeyTool>()
            .BuildServiceProvider();
        _tool = _serviceProvider.GetRequiredService<ReturnRoomKeyTool>();
        _roomKey = await SeedRoomKey();

        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.PlayerId = _roomKey.PlayerId;
        turnContext.WorldId = _worldId;
        turnContext.SessionId = _roomKey.SessionId;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Invoke_ReturnsAnOnTimeKeyWithoutCreatingAnEncounter()
    {
        await AddBooking(GameClock.Epoch + TimeSpan.FromHours(1));

        var result = await Invoke();

        Assert.Contains("\"Returned\":true", JsonSerializer.Serialize(result));
        await using var verifyContext = db.CreateContext();
        Assert.False(await verifyContext.Encounters.AnyAsync());
        Assert.False(await verifyContext.RoomBookings.AnyAsync());
        var key = await verifyContext.Items.SingleAsync(item => item.Id == _roomKey.KeyId);
        Assert.Equal(OwnerType.Workstation, key.Ownership.OwnerType);
        Assert.Equal(_roomKey.WorkstationId, key.Ownership.OwnerId);
    }

    [Fact]
    public async Task Invoke_PublishesAConfrontationForAnOverdueKey()
    {
        await AddBooking(GameClock.Epoch);

        var result = await Invoke();

        Assert.Contains("\"Confronted\":true", JsonSerializer.Serialize(result));
        await using var verifyContext = db.CreateContext();
        Assert.IsType<TheftEncounter>(await verifyContext.Encounters.SingleAsync());
        Assert.False(await verifyContext.RoomBookings.AnyAsync());
        var key = await verifyContext.Items.SingleAsync(item => item.Id == _roomKey.KeyId);
        Assert.Equal(OwnerType.Creature, key.Ownership.OwnerType);
        var events = _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents;
        Assert.Contains(events, gameEvent => gameEvent is TheftEncounterStartedEvent);
    }

    private async Task<object?> Invoke()
    {
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;
        return await invoke(_roomKey.InnkeeperName, TestContext.Current.CancellationToken);
    }

    private async Task AddBooking(GameInstant dueAtGameTime)
    {
        _context.RoomBookings.Add(
            Builders.MakeRoomBooking(
                _worldId,
                _roomKey.RoomId,
                _roomKey.KeyId,
                _roomKey.PlayerId,
                dueAtGameTime
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<RoomKeyFixture> SeedRoomKey()
    {
        var lobbyLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: _worldId, buildingType: BuildingType.Inn);
        var lobby = Builders.MakeRoom(building.Id, worldId: _worldId, locationId: lobbyLocationId);
        var location = Builders.MakeLocation(_worldId, roomId: lobby.Id, id: lobbyLocationId);
        var player = Builders.MakeCreature(_worldId, locationId: lobbyLocationId);
        var innkeeper = Builders.MakeCreature(_worldId, locationId: lobbyLocationId);
        var workstation = Builders.MakeWorkstation(
            worldId: _worldId,
            locationId: lobbyLocationId,
            ownerCreatureId: innkeeper.Id
        );
        var key = Builders.MakeKey(_worldId, ownerId: player.Id, ownerType: OwnerType.Creature);
        var session = Builders.MakeGameSession(_worldId, player.Id);

        _context.Buildings.Add(building);
        _context.Rooms.Add(lobby);
        _context.Locations.Add(location);
        _context.Creatures.AddRange(player, innkeeper);
        _context.Props.Add(workstation);
        _context.Items.Add(key);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new RoomKeyFixture(
            player.Id,
            innkeeper.Name,
            workstation.Id,
            lobby.Id,
            key.Id,
            session.Id
        );
    }

    private sealed record RoomKeyFixture(
        Guid PlayerId,
        string InnkeeperName,
        Guid WorkstationId,
        Guid RoomId,
        Guid KeyId,
        Guid SessionId
    );
}
