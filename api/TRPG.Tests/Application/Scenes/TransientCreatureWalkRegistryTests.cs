using TRPG.Application.Scenes.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes;

public sealed class TransientCreatureWalkRegistryTests
{
    [Fact]
    public void Find_ProjectsTheCurrentPositionAlongTheLocalPath()
    {
        var registry = new TransientCreatureWalkRegistry();
        var creatureId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var startedAt = new GameInstant(new DateTime(2000, 1, 1, 12, 0, 0));
        registry.Set(creatureId, locationId, [new Point(0, 0), new Point(10, 0)], startedAt, 1);

        var walk = registry.Find(creatureId, locationId, startedAt + TimeSpan.FromSeconds(4));

        Assert.NotNull(walk);
        Assert.Equal(new Point(4, 0), walk.Position);
        Assert.Equal([new Point(4, 0), new Point(10, 0)], walk.Path);
    }
}
