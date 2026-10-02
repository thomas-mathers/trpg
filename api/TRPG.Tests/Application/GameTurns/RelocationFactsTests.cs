using TRPG.Application.GameTurns;
using static TRPG.Tests.Helpers.SceneResultBuilder;

namespace TRPG.Tests.Application.GameTurns;

public sealed class RelocationFactsTests
{
    [Fact]
    public void Describe_SaysThePlayerIsAlone_WhenNobodyElseIsThere()
    {
        // Arrange — a cell the narrator was never shown is a cell it furnishes with a guard.
        var scene = MakeScene(room: "Cells", building: "The Iron Gate", others: []);

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("now in Cells in The Iron Gate", text);
        Assert.Contains("alone here", text);
    }

    [Fact]
    public void Describe_NamesEveryoneElsePresent()
    {
        // Arrange
        var scene = MakeScene(
            room: "Shop",
            building: "The Fine Filigree",
            others: ["Rowena Oakheart", "Calder Kingsley"]
        );

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("Rowena Oakheart, Calder Kingsley", text);
        Assert.DoesNotContain("alone", text);
    }

    [Fact]
    public void Describe_FallsBackToTheDistrict_WhenTheArrivalIsOutdoors()
    {
        // Arrange
        var scene = MakeScene(room: null, building: null, others: []);

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("now in The Merchant Quarter,", text);
    }

    [Fact]
    public void Describe_CarriesTheWayOut_SoTheNarratorCanPassItToATool()
    {
        // Arrange — jail relocates the player without any walk, so the model has no other source for names.
        var scene = MakeScene(
            room: "Cells",
            building: "The Iron Gate",
            others: [],
            exits: [MakeExit("Guard Station", isLocked: true)]
        );

        // Act
        var text = RelocationFacts.Describe(scene);

        // Assert
        Assert.Contains("\"destinationName\":\"Guard Station\"", text);
        Assert.Contains("\"isLocked\":true", text);
    }
}
