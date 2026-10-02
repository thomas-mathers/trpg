using TRPG.Application.GameTurns;
using TRPG.GameSessions.Responses;

namespace TRPG.Tests.Responses;

public class ActionResultTests
{
    public static TheoryData<ActionFailure> EveryFailure() => new(Enum.GetValues<ActionFailure>());

    [Theory]
    [MemberData(nameof(EveryFailure))]
    public void From_MapsEveryFailure_ToAReasonWithTheSameName(ActionFailure failure)
    {
        // Act
        var result = ActionResult.From(ActionOutcome.Failed(failure));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(failure.ToString(), result.Reason.ToString());
    }

    [Fact]
    public void From_ReportsSuccessWithoutAReason_WhenTheActionSucceeded()
    {
        // Act
        var result = ActionResult.From(ActionOutcome.Success);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(result.Reason);
    }
}
