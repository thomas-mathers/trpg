using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Queries;

public sealed class GetCreatureIdsWithCreatureJobInLocationsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private GetCreatureIdsWithCreatureJobInLocationsQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddCreatureJobCommandHandler(_context);
        _handler = new GetCreatureIdsWithCreatureJobInLocationsQueryHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsDistinctCreatureIds_AcrossAnyOfTheGivenLocations()
    {
        // Arrange
        var firstLocationId = Guid.NewGuid();
        var secondLocationId = Guid.NewGuid();
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    _creature.Id,
                    action: CreatureJobAction.Work,
                    locationId: firstLocationId
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    otherCreature.Id,
                    action: CreatureJobAction.Idle,
                    locationId: secondLocationId
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    _creature.Id,
                    action: CreatureJobAction.Sleep,
                    locationId: Guid.NewGuid()
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var creatureIds = await _handler.Handle(
            new GetCreatureIdsWithCreatureJobInLocationsQuery
            {
                LocationIds = [firstLocationId, secondLocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { _creature.Id, otherCreature.Id }.OrderBy(id => id),
            creatureIds.OrderBy(id => id)
        );
    }
}
