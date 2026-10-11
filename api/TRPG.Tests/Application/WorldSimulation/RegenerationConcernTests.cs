using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Results;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Vitals;
using TRPG.Domain;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class RegenerationConcernTests
{
    private static readonly GameInstant Start = new(new DateTime(2000, 1, 3, 12, 0, 0));
    private static readonly CreatureRegenOptions Options = new()
    {
        TickInterval = TimeSpan.FromSeconds(5),
        HpRegenPercentPerTick = 0.10f,
        ApRegenPercentPerTick = 0.10f,
        MpRegenPercentPerTick = 0.10f,
    };

    private readonly Guid _locationId = Guid.NewGuid();

    [Fact]
    public void Tick_RaisesHpByTheCompleteTicksOwed_WhenTheCreatureIsBelowMaximum()
    {
        // Arrange
        var state = MakeState(currentHp: 50);

        // Act
        var events = RegenerationConcern.Tick([state], Start + TimeSpan.FromSeconds(10), Options);

        // Assert
        var regenerated = Assert.IsType<CreatureRegenerated>(Assert.Single(events));
        Assert.Equal(70, regenerated.Vitals.CurrentHp);
        Assert.Equal(_locationId, regenerated.LocationId);
    }

    [Fact]
    public void Tick_BanksThePartialTick_WhenLessThanAFullIntervalHasPassed()
    {
        // Arrange
        var state = MakeState(currentHp: 50);

        // Act
        var events = RegenerationConcern.Tick([state], Start + TimeSpan.FromSeconds(7), Options);

        // Assert
        var regenerated = Assert.IsType<CreatureRegenerated>(Assert.Single(events));
        Assert.Equal(Start + TimeSpan.FromSeconds(5), regenerated.LastRegenAt);
    }

    [Fact]
    public void Tick_EmitsNothing_WhenNoCompleteTickHasPassed()
    {
        // Arrange
        var state = MakeState(currentHp: 50);

        // Act
        var events = RegenerationConcern.Tick([state], Start + TimeSpan.FromSeconds(4), Options);

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Tick_NeverExceedsTheMaximum()
    {
        // Arrange
        var state = MakeState(currentHp: 99);

        // Act
        var events = RegenerationConcern.Tick([state], Start + TimeSpan.FromMinutes(5), Options);

        // Assert
        var regenerated = Assert.IsType<CreatureRegenerated>(Assert.Single(events));
        Assert.Equal(100, regenerated.Vitals.CurrentHp);
    }

    private CreatureVitalState MakeState(int currentHp) =>
        new(
            new CreatureVitals(
                CreatureId: Guid.NewGuid(),
                CurrentHp: currentHp,
                MaximumHp: 100,
                CurrentAp: 10,
                MaximumAp: 10,
                CurrentMp: 10,
                MaximumMp: 10
            ),
            _locationId,
            Start
        );
}
