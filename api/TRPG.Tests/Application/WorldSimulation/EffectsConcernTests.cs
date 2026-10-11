using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Vitals;
using TRPG.Domain;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class EffectsConcernTests
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 12, 0, 0));

    private readonly Guid _locationId = Guid.NewGuid();

    [Fact]
    public void Tick_EmitsOneEventPerAdvance_CarryingTheLocationAndVitals()
    {
        // Arrange
        var advance = MakeAdvance(currentHp: 40);

        // Act
        var events = EffectsConcern.Tick([advance], Now);

        // Assert
        var advanced = Assert.Single(events);
        Assert.Equal(
            (advance.Vitals.CreatureId, _locationId, 40),
            (advanced.CreatureId, advanced.LocationId, advanced.Vitals.CurrentHp)
        );
    }

    [Fact]
    public void Settle_ReplacesTheRegeneratingVitals_WithTheEffectAdvancedOnes()
    {
        // Arrange
        var advance = MakeAdvance(currentHp: 40);
        var state = MakeState(advance.Vitals.CreatureId, currentHp: 90);
        var effects = EffectsConcern.Tick([advance], Now);

        // Act
        var settled = EffectsConcern.Settle([state], effects);

        // Assert
        Assert.Equal(40, Assert.Single(settled).Vitals.CurrentHp);
    }

    [Fact]
    public void Settle_DropsTheCreature_WhenTheEffectsKilledIt()
    {
        // Arrange
        var advance = MakeAdvance(currentHp: 0, isAlive: false);
        var state = MakeState(advance.Vitals.CreatureId, currentHp: 5);
        var effects = EffectsConcern.Tick([advance], Now);

        // Act
        var settled = EffectsConcern.Settle([state], effects);

        // Assert
        Assert.Empty(settled);
    }

    [Fact]
    public void Settle_KeepsCreaturesTheEffectsDidNotTouch()
    {
        // Arrange
        var state = MakeState(Guid.NewGuid(), currentHp: 5);

        // Act
        var settled = EffectsConcern.Settle([state], []);

        // Assert
        Assert.Equal(state, Assert.Single(settled));
    }

    private CreatureEffectAdvance MakeAdvance(int currentHp, bool isAlive = true)
    {
        var creatureId = Guid.NewGuid();
        var vitals = new CreatureVitals(creatureId, currentHp, 100, 10, 10, 10, 10);

        return new CreatureEffectAdvance(
            _locationId,
            new CreatureStateUpdate(
                creatureId,
                currentHp,
                10,
                10,
                isAlive,
                new Dictionary<string, GameInstant>(),
                new Dictionary<string, GameInstant>(),
                [],
                [],
                []
            ),
            vitals
        );
    }

    private CreatureVitalState MakeState(Guid creatureId, int currentHp) =>
        new(new CreatureVitals(creatureId, currentHp, 100, 10, 10, 10, 10), _locationId, Now);
}
