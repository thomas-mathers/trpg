using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Scenes.Navigation;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Navigation;

public class CreaturePoseResolverTests
{
    private const double Tolerance = 1e-6;

    private static readonly GameInstant EnteredAt = new(
        new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
    );

    [Fact]
    public void Resolve_ReturnsTheAnchor_WhenTheCreatureHasNoEntryWalk()
    {
        // Arrange
        var creature = MakeResult(x: 10, y: 4, angle: 1.5);

        // Act
        var pose = CreaturePoseResolver.Resolve(creature, EnteredAt, timeScale: 1);

        // Assert
        Assert.Equal(new Placement(10, 4, 1.5), pose);
    }

    [Fact]
    public void Resolve_ReturnsAPointAlongTheWalk_WhenTheWalkIsUnderway()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 1.5, entryX: 0, entryY: 0);

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            EnteredAt + TimeSpan.FromSeconds(5),
            timeScale: 1
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
    }

    [Fact]
    public void Resolve_ReturnsTheAnchorWithItsAngle_WhenTheWalkIsFinished()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 1.5, entryX: 0, entryY: 0);

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            EnteredAt + TimeSpan.FromHours(1),
            timeScale: 1
        );

        // Assert
        Assert.Equal(new Placement(30, 0, 1.5), pose);
    }

    [Fact]
    public void Resolve_WalksSlowerPerGameSecond_WhenTimePassesFasterThanReal()
    {
        // Arrange
        var creature = MakeResult(x: 30, y: 0, angle: 1.5, entryX: 0, entryY: 0);

        // Act
        var pose = CreaturePoseResolver.Resolve(
            creature,
            EnteredAt + TimeSpan.FromSeconds(50),
            timeScale: 10
        );

        // Assert
        Assert.Equal(15, pose.X, Tolerance);
    }

    private static TRPG.Application.Creatures.Results.CreatureResult MakeResult(
        double x,
        double y,
        double angle,
        double? entryX = null,
        double? entryY = null
    )
    {
        var creature = Builders.MakeCreature(x: x, y: y, angle: angle, movementSpeed: 50);
        creature.EntryX = entryX;
        creature.EntryY = entryY;
        creature.EnteredAt = entryX == null ? null : EnteredAt;

        return creature.ToResult(
            gold: 0,
            stateId: Guid.NewGuid(),
            cityId: null,
            districtId: null,
            roomId: null
        );
    }
}
