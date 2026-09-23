using TRPG.Application.Creatures;
using TRPG.Application.Encounters;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Encounters;

public class EncounterGroupInitiationResolverTests
{
    private readonly CapturingChanceRoller _chanceRoller = new();

    private static EncounterGroupCandidate MakeCandidate(
        Guid? groupId = null,
        int aggression = 0,
        int reputationSensitivity = 0,
        int riskAversion = 0,
        int reputationScore = 0,
        IReadOnlyList<int>? livingMemberLevels = null,
        EncounterApproach approach = EncounterApproach.Attack
    ) =>
        new(
            GroupId: groupId ?? Guid.NewGuid(),
            Approach: approach,
            Aggression: aggression,
            ReputationSensitivity: reputationSensitivity,
            RiskAversion: riskAversion,
            ReputationScore: reputationScore,
            LivingMemberLevels: livingMemberLevels ?? [1]
        );

    [Fact]
    public void Resolve_ReturnsNull_WhenNoCandidates()
    {
        var result = EncounterGroupInitiationResolver.Resolve(1, [], _chanceRoller);

        Assert.Null(result);
        Assert.Equal(0, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_ExcludesGroup_WhenItHasNoLivingMembers()
    {
        var candidate = MakeCandidate(aggression: 100, livingMemberLevels: []);

        var result = EncounterGroupInitiationResolver.Resolve(1, [candidate], _chanceRoller);

        Assert.Null(result);
        Assert.Equal(0, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_ExcludesGroup_WhenApproachIsNone()
    {
        var candidate = MakeCandidate(aggression: 100, approach: EncounterApproach.None);

        var result = EncounterGroupInitiationResolver.Resolve(1, [candidate], _chanceRoller);

        Assert.Null(result);
        Assert.Equal(0, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenAggressionIsZero()
    {
        var candidate = MakeCandidate(aggression: 0);

        var result = EncounterGroupInitiationResolver.Resolve(1, [candidate], _chanceRoller);

        Assert.Null(result);
        Assert.Equal(0, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_AlwaysReturnsGroup_WhenAggressionIsOneHundred()
    {
        var candidate = MakeCandidate(
            aggression: 100,
            reputationSensitivity: 100,
            riskAversion: 100,
            reputationScore: 100,
            livingMemberLevels: [1]
        );
        _chanceRoller.Result = false;

        var result = EncounterGroupInitiationResolver.Resolve(100, [candidate], _chanceRoller);

        Assert.Equal(candidate.GroupId, result);
        Assert.Equal(0, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_PrioritizesStrongestGuaranteedGroup()
    {
        var probabilistic = MakeCandidate(aggression: 99, livingMemberLevels: [100]);
        var weakerGuaranteed = MakeCandidate(aggression: 100, livingMemberLevels: [1]);
        var strongerGuaranteed = MakeCandidate(aggression: 100, livingMemberLevels: [2, 2]);

        var result = EncounterGroupInitiationResolver.Resolve(
            1,
            [probabilistic, weakerGuaranteed, strongerGuaranteed],
            _chanceRoller
        );

        Assert.Equal(strongerGuaranteed.GroupId, result);
        Assert.Equal(0, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_UsesDispositionFormula_WhenAggressionIsIntermediate()
    {
        var candidate = MakeCandidate(
            aggression: 80,
            reputationSensitivity: 50,
            riskAversion: 50,
            reputationScore: 50,
            livingMemberLevels: [2]
        );

        var result = EncounterGroupInitiationResolver.Resolve(1, [candidate], _chanceRoller);

        Assert.Equal(candidate.GroupId, result);
        Assert.Equal(0.9f, _chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_RollsOnlyForHighestScoringGroup_WhenNoGroupIsGuaranteed()
    {
        var weaker = MakeCandidate(aggression: 50);
        var stronger = MakeCandidate(aggression: 75);

        var result = EncounterGroupInitiationResolver.Resolve(1, [weaker, stronger], _chanceRoller);

        Assert.Equal(stronger.GroupId, result);
        Assert.Equal(0.75f, _chanceRoller.Chance);
        Assert.Equal(1, _chanceRoller.RollCount);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenEngagementRollFails()
    {
        var candidate = MakeCandidate(aggression: 50);
        _chanceRoller.Result = false;

        var result = EncounterGroupInitiationResolver.Resolve(1, [candidate], _chanceRoller);

        Assert.Null(result);
        Assert.Equal(0.5f, _chanceRoller.Chance);
    }

    private sealed class CapturingChanceRoller : IChanceRoller
    {
        public float? Chance { get; private set; }
        public bool Result { get; set; } = true;
        public int RollCount { get; private set; }

        public bool Roll(float chance)
        {
            Chance = chance;
            RollCount++;
            return Result;
        }
    }
}
