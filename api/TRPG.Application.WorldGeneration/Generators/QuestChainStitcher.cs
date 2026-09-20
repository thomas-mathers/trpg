namespace TRPG.Application.WorldGeneration.Generators;

public enum QuestChainBlockGenerationScope
{
    ContinuesChain,
    ConcludesChain,
}

public record QuestChainBlockSelection(QuestChainBlockType Type, int NodeCount);

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
    private static readonly IReadOnlyList<int> QuickBottleneckWidths = [1, 1];

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
                case QuestChainBlockType.SideQuest:
                    // Pass-through: attaches to the current thread but never replaces it, so
                    // whatever this selection sequence does next still continues from
                    // openNodeIds unchanged — nothing downstream ever depends on a side quest.
                    AddLinearNodes(
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
                case QuestChainBlockType.QuickBottleneck:
                    openNodeIds = AddForkNodes(
                        nodes,
                        openNodeIds,
                        selection.Type,
                        QuickBottleneckWidths,
                        exclusive: true,
                        ref nextNodeNumber,
                        ref nextGroupIndex
                    );
                    break;
                case QuestChainBlockType.BranchAndBottleneck:
                    openNodeIds = AddForkNodes(
                        nodes,
                        openNodeIds,
                        selection.Type,
                        GetBranchAndBottleneckNodeCounts(selection.NodeCount),
                        exclusive: true,
                        ref nextNodeNumber,
                        ref nextGroupIndex
                    );
                    break;
                case QuestChainBlockType.FloatingModules:
                    openNodeIds = AddForkNodes(
                        nodes,
                        openNodeIds,
                        selection.Type,
                        GetFloatingModuleNodeCounts(selection.NodeCount),
                        exclusive: false,
                        ref nextNodeNumber,
                        ref nextGroupIndex
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(selections));
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

    // Shared by QuickBottleneck, BranchAndBottleneck, and FloatingModules: each forks into
    // however many routes branchNodeCounts describes, every route starting from the same
    // prerequisites. exclusive marks the routes as alternatives (shared GroupIndex, pick one);
    // non-exclusive routes are independent and all required, same as a plain AND-join once
    // something downstream depends on every terminal.
    private static List<string> AddForkNodes(
        List<QuestChainNodeSkeleton> nodes,
        List<string> prerequisiteNodeIds,
        QuestChainBlockType blockType,
        IReadOnlyList<int> branchNodeCounts,
        bool exclusive,
        ref int nextNodeNumber,
        ref int nextGroupIndex
    )
    {
        int? groupIndex = exclusive ? nextGroupIndex++ : null;
        var terminalNodeIds = new List<string>();
        foreach (var branchNodeCount in branchNodeCounts)
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
                        blockType
                    )
                );
                currentPrerequisiteNodeIds = [nodeId];
            }
            terminalNodeIds.Add(currentPrerequisiteNodeIds.Single());
        }

        return terminalNodeIds;
    }

    private static IReadOnlyList<int> GetBranchAndBottleneckNodeCounts(int nodeCount) =>
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

    private static IReadOnlyList<int> GetFloatingModuleNodeCounts(int nodeCount) =>
        nodeCount switch
        {
            4 => [1, 1, 1, 1],
            5 => [2, 1, 1, 1],
            6 => [2, 2, 1, 1],
            7 => [2, 2, 2, 1],
            8 => [2, 2, 2, 2],
            9 => [3, 2, 2, 2],
            _ => throw new ArgumentOutOfRangeException(nameof(nodeCount)),
        };
}

public static class QuestChainBlockGraphStitcher
{
    public static IReadOnlyList<QuestChainNodeSkeleton> Stitch(QuestChainBlockGraph graph)
    {
        var nodes = new List<QuestChainNodeSkeleton>();
        var terminalNodeIdsByBlockId = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.Ordinal
        );
        var groupOffset = 0;
        var nextNodeNumber = 1;
        foreach (var block in graph.Blocks)
        {
            var localNodes = QuestChainStitcher.Stitch([
                new QuestChainBlockSelection(block.BlockType, block.NodeCount),
            ]);
            var nodeIds = localNodes.ToDictionary(
                node => node.NodeId,
                node => $"node-{nextNodeNumber++}"
            );
            var dependencyTerminalIds = block
                .DependsOnBlockIds.SelectMany(dependency => terminalNodeIdsByBlockId[dependency])
                .ToArray();
            var rebased = localNodes
                .Select(node => new QuestChainNodeSkeleton(
                    nodeIds[node.NodeId],
                    node.PrerequisiteNodeIds.Count == 0
                        ? dependencyTerminalIds
                        : node.PrerequisiteNodeIds.Select(id => nodeIds[id]).ToArray(),
                    node.GroupIndex is { } groupIndex ? groupOffset + groupIndex : null,
                    node.PrerequisiteAlternativeGroupIndex is { } prerequisiteAlternativeGroupIndex
                        ? groupOffset + prerequisiteAlternativeGroupIndex
                        : null,
                    node.FactDisclosureSupportingNodeId is { } supportId
                        ? nodeIds[supportId]
                        : null,
                    node.BlockType
                ))
                .ToArray();
            nodes.AddRange(rebased);

            // SideQuest is a pass-through at the graph level too: whatever depends on this
            // block resolves through to whatever THIS block itself depended on, so a
            // decoration never becomes a hidden prerequisite for later content.
            terminalNodeIdsByBlockId[block.Id] =
                block.BlockType == QuestChainBlockType.SideQuest
                    ? dependencyTerminalIds
                    : rebased
                        .Where(node =>
                            !rebased
                                .SelectMany(candidate => candidate.PrerequisiteNodeIds)
                                .Contains(node.NodeId)
                        )
                        .Select(node => node.NodeId)
                        .ToArray();

            groupOffset +=
                localNodes.Count == 0
                    ? 0
                    : localNodes.Max(node =>
                        Math.Max(node.GroupIndex ?? 0, node.PrerequisiteAlternativeGroupIndex ?? 0)
                    );
        }
        return nodes.ToArray();
    }
}
