using TRPG.Application.Configuration;
using TRPG.Application.Quests;

namespace TRPG.Tests.Application.Quests;

public class FactDisclosureScoreCalculatorTests
{
    private static readonly FactDisclosureOptions Options = new()
    {
        SuccessThreshold = 100,
        ReputationScoreDivisor = 2,
        GoldPerBribeScorePoint = 10,
        MinimumLevelAdvantageToIntimidate = 0,
        IntimidationScorePerLevelAdvantage = 15,
    };

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    [InlineData(1, 2, false)]
    public void CanAttemptIntimidation_ReflectsTheMinimumLevelAdvantageFloor(
        int playerLevel,
        int npcLevel,
        bool expected
    )
    {
        // Act
        var result = FactDisclosureScoreCalculator.CanAttemptIntimidation(
            playerLevel,
            npcLevel,
            Options
        );

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Score_SumsEveryComponent()
    {
        // Act
        var score = FactDisclosureScoreCalculator.Score(
            baseWillingness: 20,
            effectiveReputation: 40,
            approachContribution: 30,
            completedWeightedSupportingQuestTotal: 10,
            Options
        );

        // Assert — 20 + (40 / 2) + 30 + 10
        Assert.Equal(80, score);
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(101, true)]
    public void Succeeds_ComparesAgainstTheSuccessThreshold(int score, bool expected)
    {
        // Act
        var result = FactDisclosureScoreCalculator.Succeeds(score, Options);

        // Assert
        Assert.Equal(expected, result);
    }
}
