using TRPG.Application.GameTurns;
using static TRPG.Tests.Helpers.SceneResultBuilder;

namespace TRPG.Tests.Application.GameTurns;

public sealed class RelocationFactsTests
{
    [Fact]
    public void DescribeArrival_TellsTheModelNotToNarrateTheWalk()
    {
        // Arrange
        var scene = MakeScene(room: "Shop", building: "The Fine Filigree", others: []);

        // Act
        var text = RelocationFacts.DescribeArrival(scene);

        // Assert
        Assert.Contains("walked to Shop in The Fine Filigree", text);
        Assert.Contains("Do not describe", text);
        Assert.DoesNotContain("nothing has been narrated", text);
    }
}
