using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainBlockSequenceValidatorTests
{
    [Fact]
    public void Validate_ReturnsNull_WhenFactDisclosureIsClosedByFinale()
    {
        var selections = new QuestChainBlockSelection[]
        {
            new(QuestChainBlockType.IncitingLead, 1),
            new(QuestChainBlockType.FactDisclosure, 2),
            new(QuestChainBlockType.Finale, 1),
        };

        var error = QuestChainBlockSequenceValidator.Validate(selections, nodeBudget: 4);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenAThreadCannotBeClosedWithinBudget()
    {
        var selections = new QuestChainBlockSelection[]
        {
            new(QuestChainBlockType.IncitingLead, 1),
        };

        var error = QuestChainBlockSequenceValidator.Validate(selections, nodeBudget: 1);

        Assert.Equal(
            "IncitingLead leaves an open thread with no remaining node budget for a terminal block.",
            error
        );
    }

    [Fact]
    public void Validate_ReturnsError_WhenFinaleIsNotLast()
    {
        var selections = new QuestChainBlockSelection[]
        {
            new(QuestChainBlockType.IncitingLead, 1),
            new(QuestChainBlockType.Finale, 1),
            new(QuestChainBlockType.Investigation, 1),
        };

        var error = QuestChainBlockSequenceValidator.Validate(selections, nodeBudget: 3);

        Assert.Equal("Finale must be the last block.", error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenParallelThreadsAreClosedByAnEpilogueHook()
    {
        var selections = new QuestChainBlockSelection[]
        {
            new(QuestChainBlockType.IncitingLead, 1),
            new(QuestChainBlockType.ParallelThreads, 2),
            new(QuestChainBlockType.EpilogueHook, 1),
        };

        var error = QuestChainBlockSequenceValidator.Validate(selections, nodeBudget: 4);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenAContinuingChapterLeavesParallelThreadsOpen()
    {
        var selections = new QuestChainBlockSelection[]
        {
            new(QuestChainBlockType.IncitingLead, 1),
            new(QuestChainBlockType.Investigation, 3),
            new(QuestChainBlockType.Reversal, 2),
            new(QuestChainBlockType.ParallelThreads, 2),
        };

        var error = QuestChainBlockSequenceValidator.Validate(
            selections,
            nodeBudget: 8,
            QuestChainBlockGenerationScope.ContinuesChain
        );

        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenAContinuingChapterEndsWithAFinale()
    {
        var selections = new QuestChainBlockSelection[]
        {
            new(QuestChainBlockType.IncitingLead, 1),
            new(QuestChainBlockType.Investigation, 2),
            new(QuestChainBlockType.Finale, 1),
        };

        var error = QuestChainBlockSequenceValidator.Validate(
            selections,
            nodeBudget: 4,
            QuestChainBlockGenerationScope.ContinuesChain
        );

        Assert.Equal("Finale can only end a quest chain.", error);
    }
}
