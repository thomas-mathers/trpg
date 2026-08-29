using TRPG.Application.GameTurns;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public class StreamCombatActionTurnHandlerTests
{
    [Theory]
    [InlineData(CombatOutcome.Victory)]
    [InlineData(CombatOutcome.Defeat)]
    public void BuildNarrationPrompt_ReturnsPrompt_ForEveryTerminalOutcome(CombatOutcome outcome)
    {
        var fact = new CombatConclusionFact(outcome, ["Bandit"]);
        // Act
        var prompt = StreamCombatActionTurnHandler.BuildNarrationPrompt(fact);
        // Assert
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Theory]
    [InlineData(CombatOutcome.Ongoing)]
    [InlineData(CombatOutcome.Fled)]
    public void BuildNarrationPrompt_Throws_ForNonTerminalOrFledOutcome(CombatOutcome outcome)
    {
        var fact = new CombatConclusionFact(outcome, ["Bandit"]);
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StreamCombatActionTurnHandler.BuildNarrationPrompt(fact)
        );
    }
}
