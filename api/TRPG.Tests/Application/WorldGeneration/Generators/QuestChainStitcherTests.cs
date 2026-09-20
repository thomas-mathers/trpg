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
            new QuestChainBlockSelection(QuestChainBlockType.QuickBottleneck, 2),
            new QuestChainBlockSelection(QuestChainBlockType.Finale, 1),
        ]);

        Assert.Equal(1, nodes[1].GroupIndex);
        Assert.Equal(nodes[1].GroupIndex, nodes[2].GroupIndex);
        Assert.Equal(["node-2", "node-3"], nodes[3].PrerequisiteNodeIds);
    }

    [Fact]
    public void Stitch_ProducesFloatingModulesAndAnEpilogueHook()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.FloatingModules, 4),
            new QuestChainBlockSelection(QuestChainBlockType.EpilogueHook, 1),
        ]);

        Assert.Equal(QuestChainBlockType.IncitingLead, nodes[0].BlockType);
        Assert.All(
            nodes.Skip(1).Take(4),
            node =>
            {
                Assert.Equal(QuestChainBlockType.FloatingModules, node.BlockType);
                Assert.Null(node.GroupIndex);
                Assert.Equal(["node-1"], node.PrerequisiteNodeIds);
            }
        );
        Assert.Equal(QuestChainBlockType.EpilogueHook, nodes[5].BlockType);
        Assert.Equal(["node-2", "node-3", "node-4", "node-5"], nodes[5].PrerequisiteNodeIds);
    }

    [Fact]
    public void Stitch_ProducesExclusiveRoutesOfDifferentLengthsThatCanConverge()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.BranchAndBottleneck, 7),
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

    [Fact]
    public void Stitch_LeavesSideQuestAsAPassThrough_SoLaterBlocksSkipPastIt()
    {
        var nodes = QuestChainStitcher.Stitch([
            new QuestChainBlockSelection(QuestChainBlockType.IncitingLead, 1),
            new QuestChainBlockSelection(QuestChainBlockType.Investigation, 1),
            new QuestChainBlockSelection(QuestChainBlockType.SideQuest, 2),
            new QuestChainBlockSelection(QuestChainBlockType.Finale, 1),
        ]);

        Assert.Collection(
            nodes,
            node => Assert.Equal([], node.PrerequisiteNodeIds),
            node => Assert.Equal(["node-1"], node.PrerequisiteNodeIds),
            node =>
            {
                Assert.Equal(QuestChainBlockType.SideQuest, node.BlockType);
                Assert.Equal(["node-2"], node.PrerequisiteNodeIds);
            },
            node =>
            {
                Assert.Equal(QuestChainBlockType.SideQuest, node.BlockType);
                Assert.Equal(["node-3"], node.PrerequisiteNodeIds);
            },
            node =>
            {
                Assert.Equal(QuestChainBlockType.Finale, node.BlockType);
                // Skips past both SideQuest nodes entirely — nothing downstream ever depends
                // on optional content.
                Assert.Equal(["node-2"], node.PrerequisiteNodeIds);
            }
        );
    }
}
