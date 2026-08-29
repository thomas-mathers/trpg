using TRPG.Application.GameTurns;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public class StreamCombatConclusionNarrationTurnHandlerTests
{
    [Theory]
    [InlineData(CombatOutcome.Victory)]
    [InlineData(CombatOutcome.Defeat)]
    [InlineData(CombatOutcome.Fled)]
    public void BuildNarrationPrompt_ReturnsPrompt_ForEveryConcludedOutcome(CombatOutcome outcome)
    {
        var fact = new CombatConclusionFact(outcome, ["Bandit"]);

        // Act
        var prompt = StreamCombatConclusionNarrationTurnHandler.BuildNarrationPrompt(fact);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void BuildNarrationPrompt_Throws_ForOngoingOutcome()
    {
        var fact = new CombatConclusionFact(CombatOutcome.Ongoing, ["Bandit"]);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StreamCombatConclusionNarrationTurnHandler.BuildNarrationPrompt(fact)
        );
    }
}
