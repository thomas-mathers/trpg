using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainChapterStitcherTests
{
    [Fact]
    public void Rebase_AttachesRootsToFrontierAndNamespacesLocalReferences()
    {
        var chapter = new QuestChainGeneratedResult(
            [new QuestChainGeneratedFact("guard-secret", "Guard's secret", "A hidden route.")],
            [
                new QuestChainGeneratedNode(
                    "node-1",
                    "Meet the guard",
                    "Ask the guard.",
                    Guid.NewGuid(),
                    null,
                    1,
                    [],
                    [
                        new QuestChainGeneratedObjective(
                            "Learn the route",
                            "Get the route.",
                            GeneratedObjectiveType.LearnFactFromCreature,
                            Guid.NewGuid(),
                            null,
                            null,
                            null,
                            null,
                            1,
                            "guard-secret",
                            "guard-secret",
                            10,
                            20,
                            30,
                            [],
                            [new QuestChainGeneratedSupportingQuest("node-2", 45)]
                        ),
                    ]
                ),
                new QuestChainGeneratedNode(
                    "node-2",
                    "Help the guard",
                    "Settle the debt.",
                    Guid.NewGuid(),
                    "guard-secret",
                    null,
                    [],
                    []
                ),
            ]
        );

        var rebased = QuestChainChapterStitcher.Rebase(chapter, chapterIndex: 2, ["node-1-4"]);

        Assert.Equal("chapter-2-guard-secret", rebased.Facts.Single().Key);
        Assert.All(rebased.Nodes, node => Assert.Equal(["node-1-4"], node.PrerequisiteNodeIds));
        Assert.Equal(2001, rebased.Nodes[0].GroupIndex);
        Assert.Equal("chapter-2-guard-secret", rebased.Nodes[0].Objectives.Single().FactKey);
        Assert.Equal(
            "node-2-2",
            rebased.Nodes[0].Objectives.Single().WeightedSupportingQuestNodeIds.Single().NodeId
        );
    }
}
