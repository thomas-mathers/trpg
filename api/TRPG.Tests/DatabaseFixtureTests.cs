using Microsoft.EntityFrameworkCore;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

public sealed class DatabaseFixtureTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task InitializeAsync_IsolatesWritesAndDeletes_WhenDatabasesRunConcurrently()
    {
        // Arrange
        await using var first = new DatabaseFixture(postgres);
        await using var second = new DatabaseFixture(postgres);
        await Task.WhenAll(first.InitializeAsync().AsTask(), second.InitializeAsync().AsTask());
        await using var firstContext = first.CreateContext();
        await using var secondContext = second.CreateContext();
        var creature = Builders.MakeCreature();
        firstContext.Creatures.Add(creature);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await secondContext.Creatures.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(
            await secondContext.Creatures.ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.True(
            await firstContext.Creatures.AnyAsync(
                x => x.Id == creature.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
