using TRPG.Application.GameTurns.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public sealed class TheftEncounterPromptFactsTests
{
    private static readonly Guid TaliaId = Guid.NewGuid();

    [Fact]
    public void ToPromptFacts_SaysTheGoodsWereTheirs_WhenTheConfronterWasRobbedInPerson()
    {
        // Arrange
        var encounter = MakeEncounter(ownerCreatureId: TaliaId, ownerName: "Talia Kestrel");

        // Act
        var facts = encounter.ToPromptFacts();

        // Assert
        Assert.Equal("you", facts.StolenFrom);
    }

    [Fact]
    public void ToPromptFacts_NamesTheOwner_WhenSomeoneElseSteppedUpToConfront()
    {
        // Arrange
        var encounter = MakeEncounter(ownerCreatureId: Guid.NewGuid(), ownerName: "Morven");

        // Act
        var facts = encounter.ToPromptFacts();

        // Assert
        Assert.Equal("Morven", facts.StolenFrom);
    }

    [Fact]
    public void ToPromptFacts_ReportsNothingHeld_WhenTheAttemptWasInterrupted()
    {
        // Arrange
        var encounter = MakeEncounter(ownerCreatureId: TaliaId, ownerName: "Talia Kestrel");

        // Act
        var facts = encounter.ToPromptFacts();

        // Assert
        Assert.False(facts.ItemsHeldByPlayer);
        Assert.Equal(["Frozen Dagger", "Gold"], facts.ItemNames);
        Assert.Equal("Talia Kestrel", facts.ConfrontingName);
    }

    private static TheftEncounter MakeEncounter(Guid ownerCreatureId, string ownerName) =>
        new()
        {
            WorldId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ConfrontingCreatureId = TaliaId,
            ConfrontingName = "Talia Kestrel",
            OwnerCreatureId = ownerCreatureId,
            OwnerName = ownerName,
            ItemNames = ["Frozen Dagger", "Gold"],
        };
}
