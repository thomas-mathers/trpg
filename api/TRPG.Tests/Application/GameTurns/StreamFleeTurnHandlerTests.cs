using TRPG.Application.Abilities;
using TRPG.Application.Combat.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameTurns;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public class StreamFleeTurnHandlerTests
{
    private static readonly CombatResultPlayerState PlayerState = new(
        "Hero",
        10,
        10,
        [],
        new Dictionary<ConditionType, GameInstant>()
    );

    [Fact]
    public void BuildNarrationPrompt_DescribesStayingInPlace_WhenTheFleeAttemptHasNoDestination()
    {
        // Arrange
        var result = new FleeCombatResult(
            new CombatResult(CombatOutcome.Fled, PlayerState, [], []),
            null,
            null
        );

        // Act
        var prompt = StreamFleeTurnHandler.BuildNarrationPrompt(result);

        // Assert
        Assert.Contains("Fleeing only ends the fight", prompt, StringComparison.Ordinal);
    }
}
