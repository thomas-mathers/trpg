using TRPG.Application.WorldSimulation;
using TRPG.Domain;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class DueEntryTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private static readonly GameInstant Start = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private readonly DueEntry _entry = new(Interval);

    [Fact]
    public void TakeIfDue_IsFalse_OnTheFirstObservation()
    {
        // Act
        var due = _entry.TakeIfDue(Start);

        // Assert
        Assert.False(due);
    }

    [Fact]
    public void TakeIfDue_IsFalse_BeforeTheIntervalElapsed()
    {
        // Arrange
        _entry.TakeIfDue(Start);

        // Act
        var due = _entry.TakeIfDue(Start + Interval - TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.False(due);
    }

    [Fact]
    public void TakeIfDue_IsTrue_WhenTheIntervalElapsed()
    {
        // Arrange
        _entry.TakeIfDue(Start);

        // Act
        var due = _entry.TakeIfDue(Start + Interval);

        // Assert
        Assert.True(due);
    }

    [Fact]
    public void TakeIfDue_IsFalse_RightAfterItWasTaken()
    {
        // Arrange
        _entry.TakeIfDue(Start);
        _entry.TakeIfDue(Start + Interval);

        // Act
        var due = _entry.TakeIfDue(Start + Interval + TimeSpan.FromSeconds(1));

        // Assert
        Assert.False(due);
    }

    [Fact]
    public void TakeIfDue_RunsOnceAndReschedulesFromNow_WhenTheClockSkippedManyIntervals()
    {
        // Arrange
        var afterSkip = Start + TimeSpan.FromHours(3);
        _entry.TakeIfDue(Start);

        // Act
        var dueAfterSkip = _entry.TakeIfDue(afterSkip);

        // Assert
        Assert.True(dueAfterSkip);
        Assert.False(_entry.TakeIfDue(afterSkip + TimeSpan.FromSeconds(1)));
        Assert.True(_entry.TakeIfDue(afterSkip + Interval));
    }

    [Fact]
    public void TakeIfDue_WaitsAFullIntervalFromTheNewInstant_WhenTheClockMovedBackwards()
    {
        // Arrange
        var rewound = Start - TimeSpan.FromHours(1);
        _entry.TakeIfDue(Start);

        // Act
        var dueAtRewind = _entry.TakeIfDue(rewound);

        // Assert
        Assert.False(dueAtRewind);
        Assert.False(_entry.TakeIfDue(rewound + Interval - TimeSpan.FromMilliseconds(1)));
        Assert.True(_entry.TakeIfDue(rewound + Interval));
    }
}
