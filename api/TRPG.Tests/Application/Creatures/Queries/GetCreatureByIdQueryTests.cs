using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureByIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureByIdQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureByIdQueryHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _handler.Handle(
            new GetCreatureByIdQuery { Id = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsCreature_WhenExists()
    {
        // Act
        var result = await _handler.Handle(
            new GetCreatureByIdQuery { Id = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_creature.Id, result.Id);
    }
}
