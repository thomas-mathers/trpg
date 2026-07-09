using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Jobs.Queries;

[Collection("Database")]
public sealed class GetCreatureIdsWithJobInRoomQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddJobCommandHandler _addJob = null!;
    private Creature _creature = null!;
    private TrpgDbContext _context = null!;
    private GetCreatureIdsWithJobInRoomQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddJobCommandHandler(_context);
        _handler = new GetCreatureIdsWithJobInRoomQueryHandler(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsDistinctCreatureIds()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(_creature.Id, action: JobAction.Sleep, roomId: roomId),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(_creature.Id, action: JobAction.Work, roomId: roomId),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    otherCreature.Id,
                    action: JobAction.Sleep,
                    roomId: Guid.NewGuid()
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var creatureIds = await _handler.Handle(
            new GetCreatureIdsWithJobInRoomQuery { RoomId = roomId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var id = Assert.Single(creatureIds);
        Assert.Equal(_creature.Id, id);
    }
}
