using TRPG.Application.Narration;
using TRPG.Application.Narration.Queries;
using TRPG.Application.Narration.Results;

namespace TRPG.Tests.Application.Narration;

public class LoreAnchorLinkerTests
{
    [Fact]
    public async Task Link_PassesThroughUnchanged_WhenNoNamedEntitiesMatch()
    {
        // Arrange
        var tokens = ToAsyncEnumerable("You ", "enter ", "the ", "tavern.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(tokens, MakeMatcher(), TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal("You enter the tavern.", string.Concat(result));
    }

    [Fact]
    public async Task Link_WrapsEntityInMarkup_WhenNameMatchesExactly()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Grommash here.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains(ToMarkup(entity), result);
    }

    [Fact]
    public async Task Link_MatchesEntity_WhenNameSpansMultipleChunks()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Gro", "mmash here.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains(ToMarkup(entity), result);
    }

    [Fact]
    public async Task Link_MatchesBothEntities_WhenTwoAreMentionedInOneStream()
    {
        // Arrange
        var creature = MakeEntity("Grommash", LoreAnchorType.Creature);
        var city = MakeEntity("Ashvale", LoreAnchorType.City);
        var tokens = ToAsyncEnumerable("Grommash rides toward Ashvale.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(creature, city),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"{ToMarkup(creature)} rides toward {ToMarkup(city)}.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesEntity_RegardlessOfCase()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see GROMMASH here.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains(ToMarkup(entity), result);
    }

    [Fact]
    public async Task Link_FallsBackToPlainText_WhenPrefixNeverCompletesAMatch()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Grom without him around.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("You see Grom without him around.", string.Concat(result));
    }

    [Fact]
    public async Task Link_PreservesPunctuation_ImmediatelyAfterAnEntityMention()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("Grommash, the orc, glares at you.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"{ToMarkup(entity)}, the orc, glares at you.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesEntity_AtTheStartOfTheStream()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("Grommash enters the room.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"{ToMarkup(entity)} enters the room.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesEntity_AtTheEndOfTheStreamWithNoTrailingText()
    {
        // Arrange
        var entity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see ", "Grommash");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You see {ToMarkup(entity)}", string.Concat(result));
    }

    [Fact]
    public async Task Link_YieldsOneChunkPerWord_ForPlainTextWithNoEntities()
    {
        // Arrange
        var tokens = ToAsyncEnumerable("You enter the tavern");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(tokens, MakeMatcher(), TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(["You ", "enter ", "the ", "tavern"], result);
    }

    [Fact]
    public async Task Link_MatchesTheLongerEntity_WhenNarrationContinuesPastTheShorterName()
    {
        // Arrange
        var shortEntity = MakeEntity("Grom", LoreAnchorType.Creature);
        var longEntity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Grommash approach.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(shortEntity, longEntity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You see {ToMarkup(longEntity)} approach.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesTheShorterEntity_WhenNarrationStopsAtItsExactName()
    {
        // Arrange
        var shortEntity = MakeEntity("Grom", LoreAnchorType.Creature);
        var longEntity = MakeEntity("Grommash", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Grom approach.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(shortEntity, longEntity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You see {ToMarkup(shortEntity)} approach.", string.Concat(result));
    }

    [Fact]
    public async Task Link_ReconstructsTheFullNarration_WithEntitySpansReplacedByMarkup()
    {
        // Arrange
        var creature = MakeEntity("Grommash", LoreAnchorType.Creature);
        var city = MakeEntity("Ashvale", LoreAnchorType.City);
        var tokens = ToAsyncEnumerable(
            "You enter the tavern and spot Gro",
            "mmash sharpening his axe near the gates of Ash",
            "vale."
        );

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(creature, city),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(
            "You enter the tavern and spot "
                + $"{ToMarkup(creature)}"
                + " sharpening his axe near the gates of "
                + $"{ToMarkup(city)}"
                + ".",
            string.Concat(result)
        );
    }

    [Fact]
    public async Task Link_MatchesMultiWordEntity_WhenFullNameAppearsInNarration()
    {
        // Arrange
        var entity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You meet Alden Ashford near the gate.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You meet {ToMarkup(entity)} near the gate.", string.Concat(result));
        Assert.Contains(ToMarkup(entity), result);
    }

    [Fact]
    public async Task Link_MatchesMultiWordEntity_WhenNameSpansMultipleChunksAcrossTheInternalSpace()
    {
        // Arrange
        var entity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You meet Ald", "en Ash", "ford near the gate.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You meet {ToMarkup(entity)} near the gate.", string.Concat(result));
        Assert.Contains(ToMarkup(entity), result);
    }

    [Fact]
    public async Task Link_FallsBackToPlainText_WhenMultiWordPrefixDivergesAfterTheFirstWord()
    {
        // Arrange
        var entity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You meet Alden Baxter near the gate.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("You meet Alden Baxter near the gate.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesTheLongerMultiWordEntity_WhenNarrationContinuesPastTheShorterName()
    {
        // Arrange
        var shortEntity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var longEntity = MakeEntity("Alden Ashford Junior", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Alden Ashford Junior approach.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(shortEntity, longEntity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You see {ToMarkup(longEntity)} approach.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesTheShorterMultiWordEntity_WhenNarrationStopsAtItsExactName()
    {
        // Arrange
        var shortEntity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var longEntity = MakeEntity("Alden Ashford Junior", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You see Alden Ashford approach.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(shortEntity, longEntity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You see {ToMarkup(shortEntity)} approach.", string.Concat(result));
    }

    [Fact]
    public async Task Link_RecoversAnEarlierCompleteMatch_WhenExtensionOvershootsPastPlainWords()
    {
        // Arrange
        var shortEntity = MakeEntity("Alden", LoreAnchorType.Creature);
        var mediumEntity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var longEntity = MakeEntity("Alden Ashford Junior Senior", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("Alden Ashford Junior Bob approaches.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(shortEntity, mediumEntity, longEntity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"{ToMarkup(mediumEntity)} Junior Bob approaches.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesASecondEntity_WhenItStartsExactlyWhereALeftoverBeginsAtStreamEnd()
    {
        // Arrange
        var firstEntity = MakeEntity("Alden Ashford", LoreAnchorType.Creature);
        var secondEntity = MakeEntity("Junior", LoreAnchorType.Creature);
        var longEntity = MakeEntity("Alden Ashford Junior Senior", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("Alden Ashford Junior");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(firstEntity, secondEntity, longEntity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"{ToMarkup(firstEntity)} {ToMarkup(secondEntity)}", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesThreeWordEntityName_ForANonCreatureEntityType()
    {
        // Arrange
        var entity = MakeEntity("The Iron Anvil", LoreAnchorType.Building);
        var tokens = ToAsyncEnumerable("You step into The Iron Anvil and look around.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(entity),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You step into {ToMarkup(entity)} and look around.", string.Concat(result));
        Assert.Contains(ToMarkup(entity), result);
    }

    [Fact]
    public async Task Link_MatchesEntity_WhenAnUnrelatedEntitySharesAPrefixThenDiverges()
    {
        // Arrange
        var decoy = MakeEntity("The Gilded Anvil", LoreAnchorType.Building);
        var target = MakeEntity("Gideon", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You spot the Gideon nearby.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(decoy, target),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You spot the {ToMarkup(target)} nearby.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesMultiWordEntity_WhenAnUnrelatedEntitySharesAPrefixThenDiverges()
    {
        // Arrange
        var decoy = MakeEntity("The Gilded Anvil", LoreAnchorType.Building);
        var target = MakeEntity("Gideon Ashwood", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You spot the Gideon Ashwood nearby.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(decoy, target),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You spot the {ToMarkup(target)} nearby.", string.Concat(result));
    }

    [Fact]
    public async Task Link_MatchesEntity_WhenAnInterveningWordBreaksAFalseLeadFromAnUnrelatedEntity()
    {
        // Arrange
        var decoy = MakeEntity("The Gilded Anvil", LoreAnchorType.Building);
        var target = MakeEntity("Gideon", LoreAnchorType.Creature);
        var tokens = ToAsyncEnumerable("You spot the lone Gideon nearby.");

        // Act
        var result = await Collect(
            LoreAnchorLinker.Link(
                tokens,
                MakeMatcher(decoy, target),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal($"You spot the lone {ToMarkup(target)} nearby.", string.Concat(result));
    }

    [Fact]
    public async Task Link_DoesNotLink_WhenNameIsAmbiguous()
    {
        // Arrange
        var first = MakeEntity("The Common Name", LoreAnchorType.Building);
        var second = MakeEntity("The Common Name", LoreAnchorType.Building);
        var tokens = ToAsyncEnumerable("You see The Common Name.");

        // Act
        var linked = await LoreAnchorLinker
            .Link(tokens, MakeMatcher(first, second), TestContext.Current.CancellationToken)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("You see The Common Name.", string.Concat(linked));
    }

    private static LoreAnchorResult MakeEntity(string name, LoreAnchorType type) =>
        new(Guid.NewGuid(), name, type, Subtype: null, Description: "");

    private static LoreAnchorAutomaton MakeMatcher(params LoreAnchorResult[] entities) =>
        LoreAnchorAutomaton.Build(entities);

    private static string ToMarkup(LoreAnchorResult entity) =>
        $"[{entity.Name}](entity://{entity.Type}/{entity.Id})";

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async Task<List<string>> Collect(IAsyncEnumerable<string> items)
    {
        var collected = new List<string>();
        await foreach (var item in items)
        {
            collected.Add(item);
        }
        return collected;
    }
}
