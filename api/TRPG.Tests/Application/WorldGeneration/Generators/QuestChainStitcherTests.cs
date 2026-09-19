using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainStitcherTests
{
    [Fact]
    public void Stitch_ProducesFactDisclosurePairAndConvergingFinale()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.FactDisclosure, 2),
            new QuestChainBlockSelection(QuestChainBlockType.Finale, 1),
        ]);

        Assert.Collection(
            nodes,
            node => Assert.Equal([], node.PrerequisiteNodeIds),
            node =>
            {
                Assert.Equal(["node-1"], node.PrerequisiteNodeIds);
                Assert.Equal("node-3", node.FactDisclosureSupportingNodeId);
            },
            node => Assert.Equal(["node-1"], node.PrerequisiteNodeIds),
            node => Assert.Equal(["node-2", "node-3"], node.PrerequisiteNodeIds)
        );
    }

    [Fact]
    public void Stitch_ProducesExclusiveAlternativesThatFinaleCanRejoin()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.ExclusiveApproach, 2),
            new QuestChainBlockSelection(QuestChainBlockType.Finale, 1),
        ]);

        Assert.Equal(1, nodes[1].GroupIndex);
        Assert.Equal(nodes[1].GroupIndex, nodes[2].GroupIndex);
        Assert.Equal(["node-2", "node-3"], nodes[3].PrerequisiteNodeIds);
    }

    [Fact]
    public void Stitch_ProducesParallelThreadsAndAnEpilogueHook()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.ParallelThreads, 2),
            new QuestChainBlockSelection(QuestChainBlockType.EpilogueHook, 1),
        ]);

        Assert.Equal(QuestChainBlockType.IncitingLead, nodes[0].BlockType);
        Assert.Equal(QuestChainBlockType.ParallelThreads, nodes[1].BlockType);
        Assert.Equal(QuestChainBlockType.ParallelThreads, nodes[2].BlockType);
        Assert.Equal(QuestChainBlockType.EpilogueHook, nodes[3].BlockType);
        Assert.Equal(["node-2", "node-3"], nodes[3].PrerequisiteNodeIds);
    }

    [Fact]
    public void Stitch_ProducesExclusiveRoutesOfDifferentLengthsThatCanConverge()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.ExclusiveBranch, 7),
            new QuestChainBlockSelection(QuestChainBlockType.Finale, 1),
        ]);

        var routeGroupIndex = nodes[1].GroupIndex;
        Assert.NotNull(routeGroupIndex);
        Assert.Equal(routeGroupIndex, nodes[4].GroupIndex);
        Assert.Equal(routeGroupIndex, nodes[6].GroupIndex);
        Assert.Null(nodes[2].GroupIndex);
        Assert.Null(nodes[3].GroupIndex);
        Assert.Null(nodes[5].GroupIndex);

        Assert.Equal(routeGroupIndex, nodes[3].PrerequisiteAlternativeGroupIndex);
        Assert.Equal(routeGroupIndex, nodes[5].PrerequisiteAlternativeGroupIndex);
        Assert.Equal(routeGroupIndex, nodes[7].PrerequisiteAlternativeGroupIndex);
        Assert.Equal(["node-4", "node-6", "node-8"], nodes[8].PrerequisiteNodeIds);
    }
}
