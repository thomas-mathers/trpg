using TRPG.Application.GameTurns;

namespace TRPG.Tests.Application.GameTurns;

public class StreamCastAbilityTurnHandlerTests
{
    [Fact]
    public void BuildNarrationPrompt_IncludesTheAbilityAndTarget()
    {
        // Arrange
        var fact = new AbilityCastFact("Mend", "Restores health.", "Bystander", false);

        // Act
        var prompt = StreamCastAbilityTurnHandler.BuildNarrationPrompt(fact);

        // Assert
        Assert.Contains("Mend", prompt, StringComparison.Ordinal);
        Assert.Contains("Bystander", prompt, StringComparison.Ordinal);
    }
}
