using TRPG.Application.Creatures;
using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class HostileEncounterInitiationResolverTests
{
    private readonly CapturingChanceRoller _chanceRoller = new();

    private static HostileEncounterCandidateGroup MakeCandidate(
        Guid? groupId = null,
        int aggression = 0,
        int reputationSensitivity = 0,
        int riskAversion = 0,
        int reputationScore = 0,
        IReadOnlyList<int>? livingMemberLevels = null
    ) =>
        new(
            GroupId: groupId ?? Guid.NewGuid(),
            Aggression: aggression,
            ReputationSensitivity: reputationSensitivity,
            RiskAversion: riskAversion,
            ReputationScore: reputationScore,
            LivingMemberLevels: livingMemberLevels ?? [1]
        );

    [Fact]
    public void Resolve_ReturnsNull_WhenNoCandidates()
    {
        var result = HostileEncounterInitiationResolver.Resolve(
            playerLevel: 1,
            candidates: [],
            _chanceRoller
        );

        Assert.Null(result);
        Assert.Null(_chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_ExcludesGroup_WhenItHasNoLivingMembers()
    {
        var candidate = MakeCandidate(aggression: 100, livingMemberLevels: []);

        var result = HostileEncounterInitiationResolver.Resolve(
            playerLevel: 1,
            [candidate],
            _chanceRoller
        );

        Assert.Null(result);
        Assert.Null(_chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_ReturnsGroupId_WhenEngagementRollSucceeds()
    {
        var candidate = MakeCandidate(aggression: 50);

        var result = HostileEncounterInitiationResolver.Resolve(
            playerLevel: 1,
            [candidate],
            _chanceRoller
        );

        Assert.Equal(candidate.GroupId, result);
        Assert.Equal(0.5f, _chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenEngagementRollFails()
    {
        var candidate = MakeCandidate(aggression: 50);
        _chanceRoller.Result = false;

        var result = HostileEncounterInitiationResolver.Resolve(
            playerLevel: 1,
            [candidate],
            _chanceRoller
        );

        Assert.Null(result);
        Assert.Equal(0.5f, _chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_ReducesEngagementChance_WhenReputationIsPositive()
    {
        var candidate = MakeCandidate(
            aggression: 100,
            reputationSensitivity: 100,
            reputationScore: 50
        );

        HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate], _chanceRoller);

        Assert.Equal(0.5f, _chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_ReducesEngagementChance_WhenGroupIsWeaker()
    {
        var candidate = MakeCandidate(aggression: 100, riskAversion: 0, livingMemberLevels: [1]);

        HostileEncounterInitiationResolver.Resolve(playerLevel: 10, [candidate], _chanceRoller);

        Assert.Equal(0.1f, _chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_ClampsEngagementChance_WhenScoreExceedsThreshold()
    {
        var candidate = MakeCandidate(aggression: 100, livingMemberLevels: [10]);

        HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate], _chanceRoller);

        Assert.Equal(1.0f, _chanceRoller.Chance);
    }

    [Fact]
    public void Resolve_RollsOnlyForHighestScoringGroup_WhenMultipleGroupsAreCandidates()
    {
        var weaker = MakeCandidate(aggression: 50);
        var stronger = MakeCandidate(aggression: 75);

        var result = HostileEncounterInitiationResolver.Resolve(
            playerLevel: 1,
            [weaker, stronger],
            _chanceRoller
        );

        Assert.Equal(stronger.GroupId, result);
        Assert.Equal(0.75f, _chanceRoller.Chance);
        Assert.Equal(1, _chanceRoller.RollCount);
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
