using TRPG.Application.Abilities;
using TRPG.Application.Combat.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameTurns;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public class StreamFleeTurnHandlerTests
{
    private static readonly CombatResultPlayerState PlayerState = new(
        "Hero",
        10,
        10,
        [],
        new Dictionary<ConditionType, int>()
    );

    private static CombatResult MakeCombatResult(CombatOutcome outcome) =>
        new(outcome, PlayerState, [], []);

    [Fact]
    public void BuildNarrationPrompt_DescribesArrivingAtTheDestination_WhenTheFleeAttemptSucceeded()
    {
        // Arrange
        var result = new FleeCombatResult(
            MakeCombatResult(CombatOutcome.Fled),
            Guid.NewGuid(),
            "The Market Square"
        );

        // Act
        var prompt = StreamFleeTurnHandler.BuildNarrationPrompt(result, didMove: true);

        // Assert
        Assert.Contains(
            "carried the player to The Market Square",
            prompt,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void BuildNarrationPrompt_DescribesStayingInPlace_WhenTheFleeAttemptSucceededWithNoDestination()
    {
        // Arrange
        var result = new FleeCombatResult(MakeCombatResult(CombatOutcome.Fled), null, null);

        // Act
        var prompt = StreamFleeTurnHandler.BuildNarrationPrompt(result, didMove: false);

        // Assert
        Assert.Contains("Fleeing only ends the fight", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildNarrationPrompt_DescribesStayingInPlace_WhenMovementWasBlockedByAConfrontation()
    {
        // Arrange
        var result = new FleeCombatResult(
            MakeCombatResult(CombatOutcome.Fled),
            Guid.NewGuid(),
            "The Market Square"
        );

        // Act
        var prompt = StreamFleeTurnHandler.BuildNarrationPrompt(result, didMove: false);

        // Assert
        Assert.Contains("Fleeing only ends the fight", prompt, StringComparison.Ordinal);
    }
}
