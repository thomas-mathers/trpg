using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class AddCreatureCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddCreatureCommandHandler _handler = null!;
    private TrpgDbContext _context = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AddCreatureCommandHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsCreature()
    {
        // Arrange
        var creature = Builders.MakeCreature();

        // Act
        await _handler.Handle(
            new AddCreatureCommand { Creature = creature },
            TestContext.Current.CancellationToken
        );

        // Assert
        var found = await _context.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(found);
        Assert.Equal(creature.Name, found.Name);
    }
}
