namespace TRPG.Application.WorldGeneration.Generators;

public record QuestChainNodeSkeleton(
    string NodeId,
    IReadOnlyList<string> PrerequisiteNodeIds,
    int? GroupIndex,
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
                null,
                QuestChainBlockType.ExclusiveApproach
            )
        );
        nodes.Add(
            new QuestChainNodeSkeleton(
                secondNodeId,
                prerequisiteNodeIds,
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
                QuestChainBlockType.ParallelThreads
            )
        );
        nodes.Add(
            new QuestChainNodeSkeleton(
                secondNodeId,
                prerequisiteNodeIds,
                null,
                null,
                QuestChainBlockType.ParallelThreads
            )
        );
        return [firstNodeId, secondNodeId];
    }
}
