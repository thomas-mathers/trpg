using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class ItemModifierHelpersTests
{
    private static ModifierTemplate MakeTemplate(ModifierKey key, int weight = 1) =>
        new(MinItemLevel: 0, UniqueKey: key, Weight: weight, Build: _ => new AttributeModifier());

    [Fact]
    public void PickModifierTemplates_ReturnsEmpty_WhenPoolIsEmpty()
    {
        // Act
        var result = ItemModifierHelpers.PickModifierTemplates([], count: 3, itemLevel: 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void PickModifierTemplates_NeverReturnsMoreThanCount()
    {
        List<ModifierTemplate> pool =
        [
            MakeTemplate(ModifierKey.Strength),
            MakeTemplate(ModifierKey.Dexterity),
            MakeTemplate(ModifierKey.Intelligence),
            MakeTemplate(ModifierKey.Endurance),
        ];

        for (var i = 0; i < 20; i++)
        {
            // Act
            var result = ItemModifierHelpers.PickModifierTemplates(pool, count: 2, itemLevel: 10);

            // Assert
            Assert.True(result.Count <= 2);
        }
    }

    [Fact]
    public void PickModifierTemplates_NeverReturnsDuplicateUniqueKeys()
    {
        List<ModifierTemplate> pool =
        [
            MakeTemplate(ModifierKey.Strength),
            MakeTemplate(ModifierKey.Dexterity),
            MakeTemplate(ModifierKey.Intelligence),
        ];

        for (var i = 0; i < 20; i++)
        {
            // Act
            var result = ItemModifierHelpers.PickModifierTemplates(pool, count: 3, itemLevel: 10);

            // Assert
            Assert.Equal(result.Count, result.Select(t => t.UniqueKey).Distinct().Count());
        }
    }

    [Fact]
    public void PickModifierTemplates_StopsEarly_WhenPoolSmallerThanCount()
    {
        List<ModifierTemplate> pool = [MakeTemplate(ModifierKey.Strength)];

        // Act
        var result = ItemModifierHelpers.PickModifierTemplates(pool, count: 5, itemLevel: 10);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void BuildName_ReturnsBaseNameUnchanged_WhenNothingChosen()
    {
        // Act
        var name = ItemModifierHelpers.BuildName("Sword", []);

        // Assert
        Assert.Equal("Sword", name);
    }

    [Fact]
    public void BuildName_AddsPrefix_WhenChosenModifierHasOnlyPrefixNames()
    {
        // Act
        var name = ItemModifierHelpers.BuildName("Sword", [MakeTemplate(ModifierKey.FireDamage)]);

        // Assert
        Assert.Matches("^(Fiery|Burning|Blazing) Sword$", name);
    }

    [Fact]
    public void BuildName_AddsSuffix_WhenChosenModifierHasOnlySuffixNames()
    {
        // Act
        var name = ItemModifierHelpers.BuildName("Sword", [MakeTemplate(ModifierKey.Strength)]);

        // Assert
        Assert.Matches("^Sword of (Strength|the Titan|Might)$", name);
    }

    [Fact]
    public void BuildName_AddsPrefixAndSuffix_WhenBothAreChosen()
    {
        // Act
        var name = ItemModifierHelpers.BuildName(
            "Sword",
            [MakeTemplate(ModifierKey.FireDamage), MakeTemplate(ModifierKey.Strength)]
        );

        // Assert
        Assert.Matches("^(Fiery|Burning|Blazing) Sword of (Strength|the Titan|Might)$", name);
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(5, 0, 1)]
    [InlineData(10, 1, 2)]
    [InlineData(15, 1, 2)]
    [InlineData(20, 2, 3)]
    [InlineData(30, 2, 3)]
    [InlineData(45, 2, 4)]
    [InlineData(60, 2, 4)]
    [InlineData(75, 3, 5)]
    public void ModifierCount_StaysWithinLevelBracketRange(
        int itemLevel,
        int minimumInclusive,
        int maximumInclusive
    )
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var count = ItemModifierHelpers.ModifierCount(itemLevel);

            // Assert
            Assert.InRange(count, minimumInclusive, maximumInclusive);
        }
    }

    [Fact]
    public void Roll_NeverReturnsLessThanOne()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var roll = ItemModifierHelpers.Roll(itemLevel: 0, minimum: 0, maximum: 1);

            // Assert
            Assert.True(roll >= 1);
        }
    }

    [Fact]
    public void Roll_StaysNearMinimum_AtLowItemLevel()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var roll = ItemModifierHelpers.Roll(itemLevel: 0, minimum: 10, maximum: 20);

            // Assert — progress is 0 at level 0, leaving only up to 20% jitter above minimum
            Assert.InRange(roll, 10, 12);
        }
    }

    [Fact]
    public void Roll_StaysNearMaximum_AtHighItemLevel()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var roll = ItemModifierHelpers.Roll(itemLevel: 100, minimum: 10, maximum: 20);

            // Assert — progress is 1 at level 100, so the roll lands at or above maximum
            Assert.InRange(roll, 20, 22);
        }
    }
}
