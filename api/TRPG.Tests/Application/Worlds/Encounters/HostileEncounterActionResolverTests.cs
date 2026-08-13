using TRPG.Application.Worlds.Encounters;

namespace TRPG.Tests.Application.Worlds.Encounters;

public class HostileEncounterActionResolverTests
{
    [Fact]
    public void Resolve_ReturnsAttacked_RegardlessOfLevelsOrRoll()
    {
        // Act
        var outcome = HostileEncounterActionResolver.Resolve(
            HostileEncounterActionKind.Attack,
            playerLevel: 1,
            groupPower: 999,
            roll: 99
        );

        // Assert
        Assert.Equal(HostileEncounterActionOutcome.Attacked, outcome);
    }

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public void Resolve_ResolvesEvade_AtTheSuccessChanceBoundary_ForAnEvenMatchup(
        int roll,
        bool expectSuccess
    )
    {
        // Act — equal player level and group power yields the base 50% chance
        var outcome = HostileEncounterActionResolver.Resolve(
            HostileEncounterActionKind.Evade,
            playerLevel: 5,
            groupPower: 5,
            roll
        );

        // Assert
        Assert.Equal(
            expectSuccess
                ? HostileEncounterActionOutcome.Evaded
                : HostileEncounterActionOutcome.EvadeFailed,
            outcome
        );
    }

    [Theory]
    [InlineData(39, true)]
    [InlineData(40, false)]
    public void Resolve_ResolvesRetreat_AtTheSuccessChanceBoundary_ForAnEvenMatchup(
        int roll,
        bool expectSuccess
    )
    {
        // Act — equal player level and group power yields the base 40% chance
        var outcome = HostileEncounterActionResolver.Resolve(
            HostileEncounterActionKind.Retreat,
            playerLevel: 5,
            groupPower: 5,
            roll
        );

        // Assert
        Assert.Equal(
            expectSuccess
                ? HostileEncounterActionOutcome.Retreated
                : HostileEncounterActionOutcome.RetreatFailed,
            outcome
        );
    }

    [Fact]
    public void EvadeSuccessChance_ClampsToMaximum_WhenPlayerFarOutlevelsTheGroup()
    {
        // Act
        var chance = HostileEncounterActionResolver.EvadeSuccessChance(
            playerLevel: 20,
            groupPower: 1
        );

        // Assert
        Assert.Equal(90, chance);
    }

    [Fact]
    public void EvadeSuccessChance_ClampsToMinimum_WhenGroupFarOutlevelsThePlayer()
    {
        // Act
        var chance = HostileEncounterActionResolver.EvadeSuccessChance(
            playerLevel: 1,
            groupPower: 20
        );

        // Assert
        Assert.Equal(10, chance);
    }

    [Fact]
    public void RetreatSuccessChance_ClampsToMaximum_WhenPlayerFarOutlevelsTheGroup()
    {
        // Act
        var chance = HostileEncounterActionResolver.RetreatSuccessChance(
            playerLevel: 20,
            groupPower: 1
        );

        // Assert
        Assert.Equal(85, chance);
    }

    [Fact]
    public void RetreatSuccessChance_ClampsToMinimum_WhenGroupFarOutlevelsThePlayer()
    {
        // Act
        var chance = HostileEncounterActionResolver.RetreatSuccessChance(
            playerLevel: 1,
            groupPower: 20
        );

        // Assert
        Assert.Equal(10, chance);
    }
}
