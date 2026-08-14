using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class HostileEncounterInitiationResolverTests
{
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
        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, candidates: []);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ExcludesGroup_WhenItHasNoLivingMembers()
    {
        // Arrange
        var candidate = MakeCandidate(aggression: 500, livingMemberLevels: []);

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate]);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ReturnsGroupId_WhenEngagementScoreExceedsThreshold()
    {
        // Arrange — equal-strength matchup collapses strengthAdvantageFactor to 0, leaving
        // engagementScore == Aggression exactly, so 150 > 100 engages
        var candidate = MakeCandidate(aggression: 150, livingMemberLevels: [1]);

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate]);

        // Assert
        Assert.Equal(candidate.GroupId, result);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenEngagementScoreExactlyMeetsThreshold()
    {
        // Arrange — same equal-strength setup, but Aggression sits exactly on the threshold
        var candidate = MakeCandidate(aggression: 100, livingMemberLevels: [1]);

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate]);

        // Assert — engagement requires strictly greater than the threshold
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ReducesEngagementScore_WhenReputationIsPositiveAndSensitivityIsHigh()
    {
        // Arrange — reputationFactor collapses to 0 (1 - (100/100 * 100/100)), zeroing out an
        // otherwise-guaranteed engagement
        var candidate = MakeCandidate(
            aggression: 150,
            reputationSensitivity: 100,
            reputationScore: 100,
            livingMemberLevels: [1]
        );

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate]);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_DeclinesToEngage_WhenGroupIsWeakerAndNotRiskAverse()
    {
        // Arrange — a much weaker group (groupPower 1 vs playerLevel 10) with RiskAversion 0
        // (full strength-sensitivity) backs off even with high Aggression
        var candidate = MakeCandidate(aggression: 150, riskAversion: 0, livingMemberLevels: [1]);

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 10, [candidate]);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Engages_WhenGroupIsMuchStrongerThanThePlayer()
    {
        // Arrange — a much stronger group (groupPower 10 vs playerLevel 1) engages even with
        // modest Aggression, since the strength advantage dominates the score
        var candidate = MakeCandidate(aggression: 80, riskAversion: 0, livingMemberLevels: [10]);

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [candidate]);

        // Assert
        Assert.Equal(candidate.GroupId, result);
    }

    [Fact]
    public void Resolve_SelectsHighestScoringGroup_WhenMultipleGroupsQualify()
    {
        // Arrange
        var weaker = MakeCandidate(aggression: 150, livingMemberLevels: [1]);
        var stronger = MakeCandidate(aggression: 300, livingMemberLevels: [1]);

        // Act
        var result = HostileEncounterInitiationResolver.Resolve(playerLevel: 1, [weaker, stronger]);

        // Assert
        Assert.Equal(stronger.GroupId, result);
    }
}
