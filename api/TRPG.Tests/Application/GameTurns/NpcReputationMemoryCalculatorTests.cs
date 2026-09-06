using TRPG.Application.GameTurns;
using TRPG.Application.Reputations.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.GameTurns;

public sealed class NpcReputationMemoryCalculatorTests
{
    private static readonly Guid NpcId = Guid.NewGuid();
    private static readonly Guid FactionId = Guid.NewGuid();

    [Fact]
    public void Rank_CollapsesRepeatsOfTheSameOffence_AndCountsThem()
    {
        // Arrange
        var entries = new[]
        {
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
        };

        // Act
        var memories = NpcReputationMemoryCalculator.Rank(entries, 5);

        // Assert
        var memory = Assert.Single(memories);
        Assert.Equal(3, memory.OccurrenceCount);
    }

    [Fact]
    public void Rank_KeepsDistinctDetailsApart_SoSeparateQuestsDoNotMerge()
    {
        // Arrange
        var entries = new[]
        {
            MakeEntry(ReputationReason.QuestCompleted, 10, detail: "Found the lost ring"),
            MakeEntry(ReputationReason.QuestCompleted, 10, detail: "Cleared the cellar"),
        };

        // Act
        var memories = NpcReputationMemoryCalculator.Rank(entries, 5);

        // Assert
        Assert.Equal(2, memories.Count);
        Assert.All(memories, memory => Assert.Equal(1, memory.OccurrenceCount));
    }

    [Fact]
    public void Rank_RanksAPersonalSlightAboveAnEquallySizedFactionOne()
    {
        // Arrange
        var entries = new[]
        {
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.WitnessedTheft, -10, ReputationTargetType.Creature),
        };

        // Act
        var memories = NpcReputationMemoryCalculator.Rank(entries, 5);

        // Assert
        Assert.Equal(ReputationReason.WitnessedTheft.ToDisplayText(), memories[0].Text);
    }

    [Fact]
    public void Rank_StillRanksAFactionKillingAboveAPersonalSlight()
    {
        // Arrange — the personal multiplier must not let a small slight eclipse a killing
        var entries = new[]
        {
            MakeEntry(ReputationReason.KilledFactionMember, -100, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.WitnessedTheft, -25, ReputationTargetType.Creature),
        };

        // Act
        var memories = NpcReputationMemoryCalculator.Rank(entries, 5);

        // Assert
        Assert.Equal(ReputationReason.KilledFactionMember.ToDisplayText(), memories[0].Text);
    }

    [Fact]
    public void Rank_SumsRepeatsBeforeWeighing_SoPersistentPettyOffencesOutrankOneOffs()
    {
        // Arrange — four lock picks total -40, beating a single -25 theft
        var entries = new[]
        {
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.PickedFactionLock, -10, ReputationTargetType.Faction),
            MakeEntry(ReputationReason.StoleFromFactionMember, -25, ReputationTargetType.Faction),
        };

        // Act
        var memories = NpcReputationMemoryCalculator.Rank(entries, 5);

        // Assert
        Assert.Equal(ReputationReason.PickedFactionLock.ToDisplayText(), memories[0].Text);
    }

    [Fact]
    public void Rank_BreaksTiesTowardTheMostRecentOccurrence()
    {
        // Arrange — equal weight, different factions, so only recency separates them
        var older = MakeEntry(
            ReputationReason.PickedFactionLock,
            -10,
            ReputationTargetType.Faction,
            targetId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow.AddHours(-2)
        );
        var newer = MakeEntry(
            ReputationReason.TrespassedOnFactionProperty,
            -10,
            ReputationTargetType.Faction,
            targetId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow
        );

        // Act
        var memories = NpcReputationMemoryCalculator.Rank([older, newer], 5);

        // Assert
        Assert.Equal(
            ReputationReason.TrespassedOnFactionProperty.ToDisplayText(),
            memories[0].Text
        );
    }

    [Fact]
    public void Rank_KeepsOnlyTheStrongestMemories_WhenMoreExistThanTheLimit()
    {
        // Arrange
        var entries = Enumerable
            .Range(1, 8)
            .Select(index =>
                MakeEntry(
                    ReputationReason.PickedFactionLock,
                    -index,
                    ReputationTargetType.Faction,
                    targetId: Guid.NewGuid()
                )
            )
            .ToArray();

        // Act
        var memories = NpcReputationMemoryCalculator.Rank(entries, 5);

        // Assert
        Assert.Equal(5, memories.Count);
    }

    private static ReputationLogEntry MakeEntry(
        ReputationReason reason,
        int deltaScore,
        ReputationTargetType targetType = ReputationTargetType.Faction,
        string? detail = null,
        Guid? targetId = null,
        DateTime? createdAt = null
    ) =>
        new()
        {
            CreatureId = Guid.NewGuid(),
            Reason = reason,
            DeltaScore = deltaScore,
            TargetType = targetType,
            TargetId =
                targetId ?? (targetType == ReputationTargetType.Creature ? NpcId : FactionId),
            Detail = detail,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
}
