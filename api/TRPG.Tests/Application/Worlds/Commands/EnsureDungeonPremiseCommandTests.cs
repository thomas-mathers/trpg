using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

[Collection("Database")]
public sealed class EnsureDungeonPremiseCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly FakeChatClient _chatClient = new() { ChatResponseText = "A mine abandoned." };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<EnsureDungeonPremiseCommand> _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton(new DungeonPremiseGenerator(_chatClient))
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<EnsureDungeonPremiseCommand>
        >();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WritesAPremise_TheFirstTimeSomeoneWalksIn()
    {
        // Arrange
        var room = await SeedRoom(BuildingType.Mine);

        // Act
        await _handler.Handle(MakeCommand(room), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var building = await verifyContext.Buildings.SingleAsync(
            b => b.Id == room.BuildingId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("A mine abandoned.", building.Premise);
    }

    [Fact]
    public async Task Handle_WritesItOnce_HoweverOftenTheDungeonIsReentered()
    {
        // Arrange
        var room = await SeedRoom(BuildingType.Crypt);
        await _handler.Handle(MakeCommand(room), TestContext.Current.CancellationToken);
        _chatClient.ChatResponseText = "Something else entirely.";

        // Act
        await _handler.Handle(MakeCommand(room), TestContext.Current.CancellationToken);

        // Assert — a place does not acquire a second history on the second visit.
        await using var verifyContext = db.CreateContext();
        var building = await verifyContext.Buildings.SingleAsync(
            b => b.Id == room.BuildingId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("A mine abandoned.", building.Premise);
    }

    [Fact]
    public async Task Handle_LeavesOrdinaryBuildingsAlone()
    {
        // Arrange — an inn's history is not a mystery worth writing.
        var room = await SeedRoom(BuildingType.Inn);

        // Act
        await _handler.Handle(MakeCommand(room), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var building = await verifyContext.Buildings.SingleAsync(
            b => b.Id == room.BuildingId,
            TestContext.Current.CancellationToken
        );
        Assert.Null(building.Premise);
    }

    private static EnsureDungeonPremiseCommand MakeCommand(Room room) =>
        new() { RoomLocationId = room.LocationId };

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
