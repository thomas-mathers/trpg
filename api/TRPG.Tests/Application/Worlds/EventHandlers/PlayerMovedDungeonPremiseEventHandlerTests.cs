using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.EventHandlers;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.EventHandlers;

public sealed class PlayerMovedDungeonPremiseEventHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly FakeChatClient _chatClient = new() { ChatResponseText = "A mine abandoned." };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PlayerMovedDungeonPremiseEventHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton(new DungeonPremiseGenerator(_chatClient))
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PlayerMovedDungeonPremiseEventHandler>();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WritesAPremise_WhenTheDestinationRoomBelongsToADungeon()
    {
        // Arrange
        var room = await SeedRoom(BuildingType.Crypt);

        // Act
        await _handler.Handle(
            new PlayerMovedEvent(
                Guid.NewGuid(),
                WorldId,
                Guid.NewGuid(),
                room.LocationId,
                TimeSpan.Zero
            ),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var building = await verifyContext.Buildings.SingleAsync(
            b => b.Id == room.BuildingId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("A mine abandoned.", building.Premise);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheDestinationIsNotARoom()
    {
        // Act & Assert — a move into the wilderness or a district has no building to resolve.
        await _handler.Handle(
            new PlayerMovedEvent(
                Guid.NewGuid(),
                WorldId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                TimeSpan.Zero
            ),
            TestContext.Current.CancellationToken
        );
    }

    private async Task<Room> SeedRoom(BuildingType buildingType)
    {
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: buildingType);
        var location = Builders.MakeLocation(WorldId, Guid.NewGuid());
        var room = new Room
        {
            BuildingId = building.Id,
            LocationId = location.Id,
            Name = "Mine Shaft",
            Description = "A shaft.",
            WorldId = WorldId,
        };

        _context.Buildings.Add(building);
        _context.Locations.Add(location);
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return room;
    }
}
