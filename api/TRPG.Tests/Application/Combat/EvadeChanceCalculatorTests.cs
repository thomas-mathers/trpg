using TRPG.Application.Combat;
using TRPG.Application.Configuration;

namespace TRPG.Tests.Application.Combat;

public class EvadeChanceCalculatorTests
{
    private static readonly FleeOptions Options = new()
    {
        CatchChanceMultiplier = 0.5f,
        MinimumCatchChance = 0.05f,
        MaximumCatchChance = 0.95f,
    };

    private static EvadeParticipant MakeParticipant(
        float dexterity,
        int currentHp = 10,
        int maximumHp = 10,
        int currentAp = 10,
        int maximumAp = 10
    ) => new(dexterity, currentHp, maximumHp, currentAp, maximumAp);

    [Fact]
    public void CatchChance_ReturnsTheBaseMultiplier_ForAnEvenDexterityMatchup()
    {
        // Act — clamp(10 / 10 * 0.5, 0.05, 0.95) = 0.5
        var chance = EvadeChanceCalculator.CatchChance(
            Options,
            MakeParticipant(dexterity: 10),
            [MakeParticipant(dexterity: 10)]
        );

        // Assert
        Assert.Equal(0.5f, chance);
    }

    [Fact]
    public void CatchChance_ClampsToMaximum_WhenTheChaserIsMuchFaster()
    {
        // Act
        var chance = EvadeChanceCalculator.CatchChance(
            Options,
            MakeParticipant(dexterity: 1),
            [MakeParticipant(dexterity: 1000)]
        );

        // Assert
        Assert.Equal(0.95f, chance);
    }

    [Fact]
    public void CatchChance_ClampsToMinimum_WhenTheDefenderIsMuchFaster()
    {
        // Act
        var chance = EvadeChanceCalculator.CatchChance(
            Options,
            MakeParticipant(dexterity: 1000),
            [MakeParticipant(dexterity: 1)]
        );

        // Assert
        Assert.Equal(0.05f, chance);
    }

    [Fact]
    public void CatchChance_ReturnsTheMinimum_WhenThereAreNoChasers()
    {
        // Act
        var chance = EvadeChanceCalculator.CatchChance(Options, MakeParticipant(dexterity: 10), []);

        // Assert
        Assert.Equal(0.05f, chance);
    }

    [Fact]
    public void CatchChance_UsesTheFastestChaser_NotTheirRawAverage()
    {
        // Arrange — a slow chaser alongside a fast one shouldn't drag the group average down
        var slowChaser = MakeParticipant(dexterity: 1);
        var fastChaser = MakeParticipant(dexterity: 10);

        // Act
        var chance = EvadeChanceCalculator.CatchChance(
            Options,
            MakeParticipant(dexterity: 10),
            [slowChaser, fastChaser]
        );

        // Assert — same as a single 10-Dexterity chaser: clamp(10/10*0.5, ...) = 0.5
        Assert.Equal(0.5f, chance);
    }

    [Fact]
    public void CatchChance_ReducesEffectiveDexterity_ForAWoundedParticipant()
    {
        // Arrange — 100 Dexterity at 10% HP (full AP) behaves like 10 Dexterity
        var woundedChaser = MakeParticipant(dexterity: 100, currentHp: 1, maximumHp: 10);

        // Act
        var chance = EvadeChanceCalculator.CatchChance(
            Options,
            MakeParticipant(dexterity: 10),
            [woundedChaser]
        );

        // Assert
        Assert.Equal(0.5f, chance);
    }

    [Fact]
    public void CatchChance_ReducesEffectiveDexterity_ForAnExhaustedParticipant()
    {
        // Arrange — 100 Dexterity at 10% AP (full HP) behaves like 10 Dexterity;
        // the worse of the two resources gates the effective value, not their average
        var exhaustedChaser = MakeParticipant(dexterity: 100, currentAp: 1, maximumAp: 10);

        // Act
        var chance = EvadeChanceCalculator.CatchChance(
            Options,
            MakeParticipant(dexterity: 10),
            [exhaustedChaser]
        );

        // Assert
        Assert.Equal(0.5f, chance);
    }
}
