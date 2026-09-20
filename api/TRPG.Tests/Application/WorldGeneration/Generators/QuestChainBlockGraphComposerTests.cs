using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainBlockGraphComposerTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(34)]
    public void Compose_NeverExceedsTheNodeBudget(int nodeBudget)
    {
        var composer = new QuestChainBlockGraphComposer(new Random(42));

        var graph = composer.Compose(nodeBudget, nodeBudget);

        Assert.True(graph.Blocks.Sum(block => block.NodeCount) <= nodeBudget);
    }

    [Fact]
    public void Compose_StartsWithIncitingLeadAndEndsWithATerminalBlock()
    {
        var composer = new QuestChainBlockGraphComposer(new Random(7));

        var graph = composer.Compose(20, 20);

        Assert.Equal(QuestChainBlockType.IncitingLead, graph.Blocks[0].BlockType);
        Assert.True(
            graph.Blocks[^1].BlockType
                is QuestChainBlockType.Finale
                    or QuestChainBlockType.EpilogueHook
        );
    }

    [Fact]
    public void Compose_IncludesABranchAndBottleneck_WhenBudgetIsAtLeastSixteen()
    {
        var composer = new QuestChainBlockGraphComposer(new Random(3));

        var graph = composer.Compose(20, 20);

        Assert.Contains(
            graph.Blocks,
            block => block.BlockType == QuestChainBlockType.BranchAndBottleneck
        );
    }

    [Fact]
    public void Compose_OmitsABranchAndBottleneck_WhenBudgetIsBelowSixteen()
    {
        var composer = new QuestChainBlockGraphComposer(new Random(3));

        var graph = composer.Compose(10, 10);

        Assert.DoesNotContain(
            graph.Blocks,
            block => block.BlockType == QuestChainBlockType.BranchAndBottleneck
        );
    }

    [Fact]
    public void Compose_NeverPlacesTwoWeightedShapeBlocksConsecutively()
    {
        // BranchAndBottleneck is placed through a separate mandatory-guarantee path (covered by
        // Compose_IncludesABranchAndBottleneck_WhenBudgetIsAtLeastSixteen) that can rarely end up
        // adjacent to a shape block as a last resort, when there's no budget left to defer it —
        // this only covers the two types chosen by the general weighted walk, which never
        // repeats one of them immediately after the other.
        var weightedShapeTypes = new[]
        {
            QuestChainBlockType.QuickBottleneck,
            QuestChainBlockType.FloatingModules,
        };
        var composer = new QuestChainBlockGraphComposer(new Random(19));

        var graph = composer.Compose(34, 34);

        for (var index = 1; index < graph.Blocks.Count; index++)
        {
            Assert.False(
                weightedShapeTypes.Contains(graph.Blocks[index - 1].BlockType)
                    && weightedShapeTypes.Contains(graph.Blocks[index].BlockType)
            );
        }
    }

    [Fact]
    public void Compose_WiresEachBlockToDependOnlyOnTheImmediatelyPrecedingBlock()
    {
        var composer = new QuestChainBlockGraphComposer(new Random(11));

        var graph = composer.Compose(24, 24);

        Assert.Equal([], graph.Blocks[0].DependsOnBlockIds);
        for (var index = 1; index < graph.Blocks.Count; index++)
        {
            Assert.Equal([graph.Blocks[index - 1].Id], graph.Blocks[index].DependsOnBlockIds);
        }
    }
}
