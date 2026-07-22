using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Queries;

[Collection("Database")]
public sealed class GetCreatureIdsWithCreatureJobInRoomQueryTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private GetCreatureIdsWithCreatureJobInRoomQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddCreatureJobCommandHandler(_context);
        _handler = new GetCreatureIdsWithCreatureJobInRoomQueryHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    _creature.Id,
                    action: CreatureJobAction.Sleep,
                    roomId: roomId
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    _creature.Id,
                    action: CreatureJobAction.Work,
                    roomId: roomId
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    otherCreature.Id,
                    action: CreatureJobAction.Sleep,
                    roomId: Guid.NewGuid()
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var creatureIds = await _handler.Handle(
            new GetCreatureIdsWithCreatureJobInRoomQuery { RoomId = roomId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var id = Assert.Single(creatureIds);
        Assert.Equal(_creature.Id, id);
    }
}
