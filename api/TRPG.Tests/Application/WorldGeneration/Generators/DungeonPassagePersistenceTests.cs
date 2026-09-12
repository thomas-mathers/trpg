using Microsoft.EntityFrameworkCore;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public sealed class DungeonPassagePersistenceTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task SaveChanges_PreservesBothDirections_WhenPersistingGeneratedPassages()
    {
        // Arrange
        await using var context = database.CreateContext();
        var worldId = Guid.NewGuid();
        var dungeon = DungeonGenerator.Generate(
            new DungeonGeneratorInput(
                [],
                new Location
                {
                    StateId = Guid.NewGuid(),
                    WorldId = worldId,
                    Kind = LocationKind.Wilderness,
                },
                worldId
            )
            {
                Random = new Random(7),
            }
        );
        var expectedPaths = dungeon
            .LocationConnectors.Where(connector => connector.Path != null)
            .ToDictionary(connector => connector.Id, connector => connector.Path!.Points.ToArray());
        context.LocationConnectors.AddRange(dungeon.LocationConnectors);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var verification = database.CreateContext();
        var persisted = await verification
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId && connector.Path != null)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedPaths.Count, persisted.Length);
        Assert.All(
            persisted,
            connector => Assert.Equal(expectedPaths[connector.Id], connector.Path!.Points)
        );
    }
}
