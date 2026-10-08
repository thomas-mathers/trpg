using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public class DepartureTimingTests
{
    private static readonly TimeSpan Walk = TimeSpan.FromMinutes(10);
    private static readonly GameInstant Noon = new(new DateTime(2000, 1, 3, 12, 0, 0));

    private readonly Guid _creatureId = Guid.NewGuid();

    [Fact]
    public void Resolve_ReturnsTheSameInstant_WhenCalledTwiceForTheSameTransition()
    {
        // Arrange
        var transition = Transition(CreatureJobAction.Eat, CreatureJobAction.Work);
        var first = DepartureTiming.Resolve(_creatureId, transition, Walk);

        // Act
        var second = DepartureTiming.Resolve(_creatureId, transition, Walk);

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void Resolve_StaysWithinTwentyMinutesOfTheBaseTime_WhenGoingToLunch()
    {
        // Arrange
        var transition = Transition(CreatureJobAction.Eat, CreatureJobAction.Work);

        // Act
        var offsets = ResolveForManyCreatures(transition)
            .Select(departure => (departure - (Noon - Walk)).TotalMinutes)
            .ToArray();

        // Assert
        Assert.InRange(offsets.Min(), -20, -10);
        Assert.InRange(offsets.Max(), 10, 20);
    }

    [Fact]
    public void Resolve_DepartsUpToThirtyMinutesAfterTheShiftEnds_WhenLeavingWork()
    {
        // Arrange
        var transition = Transition(CreatureJobAction.Sleep, CreatureJobAction.Work);

        // Act
        var offsets = ResolveForManyCreatures(transition)
            .Select(departure => (departure - Noon).TotalMinutes)
            .ToArray();

        // Assert
        Assert.InRange(offsets.Min(), 0, 5);
        Assert.InRange(offsets.Max(), 25, 30);
    }

    private static IEnumerable<GameInstant> ResolveForManyCreatures(JobTransition transition) =>
        Enumerable
            .Range(1, 200)
            .Select(seed =>
                DepartureTiming.Resolve(new Guid(seed, 0, 0, new byte[8]), transition, Walk)
            );

    private JobTransition Transition(CreatureJobAction destination, CreatureJobAction origin) =>
        new(
            Builders.MakeCreatureJob(_creatureId, action: destination),
            Builders.MakeCreatureJob(_creatureId, action: origin),
            Noon,
            false
        );
}
