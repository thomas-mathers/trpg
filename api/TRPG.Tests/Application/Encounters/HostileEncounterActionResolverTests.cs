using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class HostileEncounterActionResolverTests
{
    private static readonly FleeOptions Options = new();

    private static EvadeParticipant MakeParticipant(
        float dexterity,
        int currentHp = 10,
        int maximumHp = 10,
        int currentAp = 10,
        int maximumAp = 10
    ) => new(dexterity, currentHp, maximumHp, currentAp, maximumAp);

    [Fact]
    public void Resolve_ReturnsAttacked_RegardlessOfParticipantsOrRoll()
    {
        // Act
        var outcome = HostileEncounterActionResolver.Resolve(
            new AttackEncounterAction(),
            Options,
            MakeParticipant(dexterity: 1),
            [MakeParticipant(dexterity: 999)],
            roll: 0.99
        );

        // Assert
        Assert.Equal(HostileEncounterResolutionOutcome.Attacked, outcome);
    }

    [Theory]
    [InlineData(0.49, false)]
    [InlineData(0.5, true)]
    public void Resolve_ResolvesEvade_AtTheCatchChanceBoundary_ForAnEvenMatchup(
        double roll,
        bool expectSuccess
    )
    {
        // Act — equal Dexterity yields the base 50% catch chance
        var outcome = HostileEncounterActionResolver.Resolve(
            new EvadeEncounterAction(),
            Options,
            MakeParticipant(dexterity: 10),
            [MakeParticipant(dexterity: 10)],
            roll
        );

        // Assert
        Assert.Equal(
            expectSuccess
                ? HostileEncounterResolutionOutcome.Evaded
                : HostileEncounterResolutionOutcome.EvadeFailed,
            outcome
        );
    }

    [Theory]
    [InlineData(0.49, false)]
    [InlineData(0.5, true)]
    public void Resolve_ResolvesRetreat_AtTheCatchChanceBoundary_ForAnEvenMatchup(
        double roll,
        bool expectSuccess
    )
    {
        // Act — equal Dexterity yields the base 50% catch chance
        var outcome = HostileEncounterActionResolver.Resolve(
            new RetreatEncounterAction(),
            Options,
            MakeParticipant(dexterity: 10),
            [MakeParticipant(dexterity: 10)],
            roll
        );

        // Assert
        Assert.Equal(
            expectSuccess
                ? HostileEncounterResolutionOutcome.Retreated
                : HostileEncounterResolutionOutcome.RetreatFailed,
            outcome
        );
    }

    [Fact]
    public void Resolve_UsesTheFastestGroupMember_WhenComparingAgainstThePlayer()
    {
        // Arrange — the slow member alone would yield a low catch chance, but the fast member
        // in the same group should dominate the comparison
        var slowMember = MakeParticipant(dexterity: 1);
        var fastMember = MakeParticipant(dexterity: 100);

        // Act
        var outcome = HostileEncounterActionResolver.Resolve(
            new EvadeEncounterAction(),
            Options,
            MakeParticipant(dexterity: 10),
            [slowMember, fastMember],
            roll: 0.94
        );

        // Assert — clamp(100/10*0.5, 0.05, 0.95) = 0.95, so a 0.94 roll is still caught
        Assert.Equal(HostileEncounterResolutionOutcome.EvadeFailed, outcome);
    }

    [Fact]
    public void Resolve_ReducesAGroupMembersEffectiveDexterity_WhenItIsWounded()
    {
        // Arrange — 100 Dexterity at 10% HP behaves like 10 Dexterity
        var woundedMember = MakeParticipant(dexterity: 100, currentHp: 1, maximumHp: 10);

        // Act
        var outcome = HostileEncounterActionResolver.Resolve(
            new EvadeEncounterAction(),
            Options,
            MakeParticipant(dexterity: 10),
            [woundedMember],
            roll: 0.5
        );

        // Assert — clamp(10/10*0.5, 0.05, 0.95) = 0.5, so a 0.5 roll is not caught
        Assert.Equal(HostileEncounterResolutionOutcome.Evaded, outcome);
    }
}
