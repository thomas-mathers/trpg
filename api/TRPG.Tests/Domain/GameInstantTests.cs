using TRPG.Domain;

namespace TRPG.Tests.Domain;

public class GameInstantTests
{
    private static readonly GameInstant Instant = new(
        new DateTime(975, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
    );

    [Fact]
    public void Constructor_PreservesValue_WhenKindIsUnspecified()
    {
        var value = new DateTime(975, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);

        var instant = new GameInstant(value);

        Assert.Equal(value, instant.Value);
        Assert.Equal(DateTimeKind.Unspecified, instant.Value.Kind);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void Constructor_Throws_WhenKindIsNotUnspecified(DateTimeKind kind)
    {
        var value = DateTime.SpecifyKind(Instant.Value, kind);

        var exception = Assert.Throws<ArgumentException>(() => new GameInstant(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Addition_ReturnsLaterInstant()
    {
        var result = Instant + TimeSpan.FromMinutes(90);

        Assert.Equal(new DateTime(975, 1, 1, 9, 30, 0), result.Value);
    }

    [Fact]
    public void ReverseAddition_ReturnsLaterInstant()
    {
        var result = TimeSpan.FromMinutes(90) + Instant;

        Assert.Equal(new DateTime(975, 1, 1, 9, 30, 0), result.Value);
    }

    [Fact]
    public void DurationSubtraction_ReturnsEarlierInstant()
    {
        var result = Instant - TimeSpan.FromMinutes(90);

        Assert.Equal(new DateTime(975, 1, 1, 6, 30, 0), result.Value);
    }

    [Fact]
    public void InstantSubtraction_ReturnsDuration()
    {
        var later = Instant + TimeSpan.FromMinutes(90);

        var result = later - Instant;

        Assert.Equal(TimeSpan.FromMinutes(90), result);
    }

    [Fact]
    public void ComparisonOperators_OrderByDateTimeValue()
    {
        var later = Instant + TimeSpan.FromTicks(1);
        var same = new GameInstant(Instant.Value);

        Assert.True(Instant < later);
        Assert.True(Instant <= later);
        Assert.True(later > Instant);
        Assert.True(later >= Instant);
        Assert.True(Instant <= same);
        Assert.True(Instant >= same);
        Assert.True(Instant.CompareTo(later) < 0);
    }
}
