namespace TRPG.Application.WorldGeneration.Generators;

public record QuestChainNodeSkeleton(
    string NodeId,
    IReadOnlyList<string> PrerequisiteNodeIds,
    int? GroupIndex,
    int? PrerequisiteAlternativeGroupIndex,
    string? FactDisclosureSupportingNodeId,
    QuestChainBlockType BlockType
);

public static class QuestChainStitcher
{
    public static IReadOnlyList<QuestChainNodeSkeleton> Stitch(
        IReadOnlyList<QuestChainBlockSelection> selections
    )
    {
        var nodes = new List<QuestChainNodeSkeleton>();
        var openNodeIds = new List<string>();
        var nextNodeNumber = 1;
        var nextGroupIndex = 1;

        foreach (var selection in selections)
        {
            switch (selection.Type)
            {
                case QuestChainBlockType.IncitingLead:
                case QuestChainBlockType.Investigation:
                case QuestChainBlockType.Escalation:
                case QuestChainBlockType.Reversal:
                case QuestChainBlockType.Favor:
                case QuestChainBlockType.Finale:
                case QuestChainBlockType.EpilogueHook:
                    openNodeIds = AddLinearNodes(
                        nodes,
                        openNodeIds,
                        selection.NodeCount,
                        selection.Type,
                        ref nextNodeNumber
                    );
                    break;
                case QuestChainBlockType.FactDisclosure:
                    openNodeIds = AddFactDisclosureNodes(nodes, openNodeIds, ref nextNodeNumber);
                    break;
                case QuestChainBlockType.ExclusiveApproach:
                    openNodeIds = AddExclusiveApproachNodes(
                        nodes,
                        openNodeIds,
                        ref nextNodeNumber,
                        ref nextGroupIndex
                    );
                    break;
                case QuestChainBlockType.ExclusiveBranch:
                    openNodeIds = AddExclusiveBranchNodes(
                        nodes,
                        openNodeIds,
                        selection.NodeCount,
                        ref nextNodeNumber,
                        ref nextGroupIndex
                    );
                    break;
                case QuestChainBlockType.ParallelThreads:
                    openNodeIds = AddParallelThreadNodes(nodes, openNodeIds, ref nextNodeNumber);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(selection));
            }
        }

        return nodes.ToArray();
    }

    private static List<string> AddLinearNodes(
        List<QuestChainNodeSkeleton> nodes,
        List<string> prerequisiteNodeIds,
        int nodeCount,
        QuestChainBlockType blockType,
        ref int nextNodeNumber
    )
    {
        var currentPrerequisiteNodeIds = prerequisiteNodeIds;
        for (var index = 0; index < nodeCount; index++)
        {
            var nodeId = $"node-{nextNodeNumber++}";
            nodes.Add(
                new QuestChainNodeSkeleton(
                    nodeId,
                    currentPrerequisiteNodeIds,
                    null,
                    null,
                    null,
                    blockType
                )
            );
            currentPrerequisiteNodeIds = [nodeId];
        }

        return currentPrerequisiteNodeIds;
    }

    private static List<string> AddFactDisclosureNodes(
        List<QuestChainNodeSkeleton> nodes,
        List<string> prerequisiteNodeIds,
        ref int nextNodeNumber
    )
    {
        var primaryNodeId = $"node-{nextNodeNumber++}";
        var supportingNodeId = $"node-{nextNodeNumber++}";
        nodes.Add(
            new QuestChainNodeSkeleton(
                primaryNodeId,
                prerequisiteNodeIds,
                null,
                null,
                supportingNodeId,
                QuestChainBlockType.FactDisclosure
            )
        );
        nodes.Add(
            new QuestChainNodeSkeleton(
                supportingNodeId,
                prerequisiteNodeIds,
                null,
                null,
                null,
                QuestChainBlockType.FactDisclosure
            )
        );
        return [primaryNodeId, supportingNodeId];
    }

    private static List<string> AddExclusiveApproachNodes(
        List<QuestChainNodeSkeleton> nodes,
        List<string> prerequisiteNodeIds,
        ref int nextNodeNumber,
        ref int nextGroupIndex
    )
    {
        var groupIndex = nextGroupIndex++;
        var firstNodeId = $"node-{nextNodeNumber++}";
        var secondNodeId = $"node-{nextNodeNumber++}";
        nodes.Add(
            new QuestChainNodeSkeleton(
                firstNodeId,
                prerequisiteNodeIds,
                groupIndex,
                groupIndex,
                null,
                QuestChainBlockType.ExclusiveApproach
            )
        );
        nodes.Add(
            new QuestChainNodeSkeleton(
                secondNodeId,
                prerequisiteNodeIds,
                groupIndex,
                groupIndex,
                null,
                QuestChainBlockType.ExclusiveApproach
            )
        );
        return [firstNodeId, secondNodeId];
    }

    private static List<string> AddParallelThreadNodes(
        List<QuestChainNodeSkeleton> nodes,
        List<string> prerequisiteNodeIds,
        ref int nextNodeNumber
    )
    {
        var firstNodeId = $"node-{nextNodeNumber++}";
        var secondNodeId = $"node-{nextNodeNumber++}";
        nodes.Add(
            new QuestChainNodeSkeleton(
                firstNodeId,
                prerequisiteNodeIds,
                null,
                null,
                null,
                QuestChainBlockType.ParallelThreads
            )
        );
        nodes.Add(
            new QuestChainNodeSkeleton(
                secondNodeId,
                prerequisiteNodeIds,
                null,
                null,
                null,
                QuestChainBlockType.ParallelThreads
            )
        );
        return [firstNodeId, secondNodeId];
    }

    private static List<string> AddExclusiveBranchNodes(
        List<QuestChainNodeSkeleton> nodes,
        List<string> prerequisiteNodeIds,
        int nodeCount,
        ref int nextNodeNumber,
        ref int nextGroupIndex
    )
    {
        var groupIndex = nextGroupIndex++;
        var terminalNodeIds = new List<string>();
        foreach (var branchNodeCount in GetExclusiveBranchNodeCounts(nodeCount))
        {
            var currentPrerequisiteNodeIds = prerequisiteNodeIds;
            for (var index = 0; index < branchNodeCount; index++)
            {
                var nodeId = $"node-{nextNodeNumber++}";
                nodes.Add(
                    new QuestChainNodeSkeleton(
                        nodeId,
                        currentPrerequisiteNodeIds,
                        index == 0 ? groupIndex : null,
                        index == branchNodeCount - 1 ? groupIndex : null,
                        null,
                        QuestChainBlockType.ExclusiveBranch
                    )
                );
                currentPrerequisiteNodeIds = [nodeId];
            }
            terminalNodeIds.Add(currentPrerequisiteNodeIds.Single());
        }

        return terminalNodeIds;
    }

    private static IReadOnlyList<int> GetExclusiveBranchNodeCounts(int nodeCount) =>
        nodeCount switch
        {
            5 => [3, 2],
            6 => [4, 2],
            7 => [3, 2, 2],
            8 => [4, 2, 2],
            9 => [4, 3, 2],
            10 => [4, 2, 2, 2],
            11 => [4, 3, 2, 2],
            12 => [4, 3, 3, 2],
            _ => throw new ArgumentOutOfRangeException(nameof(nodeCount)),
        };
}
