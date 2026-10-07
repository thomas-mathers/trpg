using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Scenes.Queries;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Queries;

public sealed class GetCreatureWalkPathsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCreatureWalkPathsQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCreatureWalkPathsQueryHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RoutesAroundFurniture_WhenTheLocationIsARoom()
    {
        // Arrange
        var room = Builders.MakeLocation(
            worldId: _worldId,
            kind: LocationKind.Room,
            width: 10,
            depth: 10
        );
        var table = new Furniture
        {
            Model = PropModel.FurnitureTable,
            LocationId = room.Id,
            WorldId = _worldId,
            X = 5,
            Y = 5,
            Width = 2,
            Depth = 2,
        };
        _context.Locations.Add(room);
        _context.Props.Add(table);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var walker = MakeWalker(room.Id, anchorX: 8.75, entry: new Point(1.25, 5));

        // Act
        var paths = await _handler.Handle(
            new GetCreatureWalkPathsQuery { LocationId = room.Id, Creatures = [walker] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(paths[walker.Id].Count > 2);
    }

    [Fact]
    public async Task Handle_WalksStraight_WhenTheDistrictHasNoRoads()
    {
        // Arrange
        var district = await SeedRoadlessDistrict();
        var walker = MakeWalker(district.Id, anchorX: 8.75, entry: new Point(1.25, 5));

        // Act
        var paths = await _handler.Handle(
            new GetCreatureWalkPathsQuery { LocationId = district.Id, Creatures = [walker] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([new Point(1.25, 5), new Point(8.75, 5)], paths[walker.Id]);
    }

    [Fact]
    public async Task Handle_SkipsCreaturesWithoutAnEntryWalk()
    {
        // Arrange
        var creature = Builders
            .MakeCreature(worldId: _worldId, x: 3, y: 3)
            .ToResult(0, Guid.NewGuid(), null, null, null);

        // Act
        var paths = await _handler.Handle(
            new GetCreatureWalkPathsQuery { LocationId = Guid.NewGuid(), Creatures = [creature] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(paths);
    }

    [Fact]
    public async Task Handle_WalksFromTheAnchorToTheExit_WhenTheNpcIsLeaving()
    {
        // Arrange
        var district = await SeedRoadlessDistrict();
        var leaver = MakeWalker(district.Id, anchorX: 2.5, exit: new Point(8.75, 5));

        // Act
        var paths = await _handler.Handle(
            new GetCreatureWalkPathsQuery { LocationId = district.Id, Creatures = [leaver] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([new Point(2.5, 5), new Point(8.75, 5)], paths[leaver.Id]);
    }

    [Fact]
    public async Task Handle_WalksFromTheEntryToTheExit_WhenTheNpcIsPassingThrough()
    {
        // Arrange
        var district = await SeedRoadlessDistrict();
        var traveler = MakeWalker(
            district.Id,
            anchorX: 5,
            entry: new Point(1.25, 5),
            exit: new Point(8.75, 5)
        );

        // Act
        var paths = await _handler.Handle(
            new GetCreatureWalkPathsQuery { LocationId = district.Id, Creatures = [traveler] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([new Point(1.25, 5), new Point(8.75, 5)], paths[traveler.Id]);
    }

    private async Task<Location> SeedRoadlessDistrict()
    {
        var district = Builders.MakeLocation(
            worldId: _worldId,
            kind: LocationKind.District,
            width: 10,
            depth: 10
        );
        _context.Locations.Add(district);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return district;
    }

    private CreatureResult MakeWalker(
        Guid locationId,
        double anchorX,
        Point? entry = null,
        Point? exit = null
    )
    {
        var departedAt = new GameInstant(new DateTime(2026, 1, 1));
        var creature = Builders.MakeCreature(
            worldId: _worldId,
            locationId: locationId,
            x: anchorX,
            y: 5,
            entry: entry,
            enteredAt: entry == null ? null : departedAt,
            exit: exit,
            departedAt: exit == null ? null : departedAt
        );

        return creature.ToResult(0, Guid.NewGuid(), null, null, null);
    }
}
