using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;

namespace TRPG.Application.WorldGeneration.Generators;

internal class QuestChainBlockGraphSchema
{
    public List<QuestChainBlockGraphBlockSchema> Blocks { get; init; } = [];
}

internal class QuestChainBlockGraphBlockSchema
{
    public string Id { get; init; } = "";
    public string BlockType { get; init; } = "";
    public int NodeCount { get; init; }
    public List<string> DependsOnBlockIds { get; init; } = [];
}

public record QuestChainBlockGraph(IReadOnlyList<QuestChainBlockGraphBlock> Blocks);

public record QuestChainBlockGraphBlock(
    string Id,
    QuestChainBlockType BlockType,
    int NodeCount,
    IReadOnlyList<string> DependsOnBlockIds
);

public class QuestChainBlockGraphGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainBlockGraphGenerator> logger
)
{
    // Three live generation runs each landed at 53-63% of a 32-node budget — not story-driven
    // restraint, a systematic undershoot from a prompt that only ever guarded against padding.
    // This floor is a deliberate push back toward the budget, not a measurement of what's typical.
    private const double MinimumBudgetUsage = 0.6;

    // ExclusiveBranch is the only block type whose alternative routes diverge for more than one
    // node before reconverging; ExclusiveApproach is locked to exactly one node per side. Without
    // a floor, two of three live runs never used it, so every choice they offered reconverged
    // immediately and never compounded into something bigger.
    private const int MinimumBudgetForExclusiveBranch = 16;

    public async Task<QuestChainBlockGraph> Generate(
        QuestChainStory story,
        int nodeBudget,
        CancellationToken cancellationToken = default
    )
    {
        var schema = await client.GetValidatedJson<QuestChainBlockGraphSchema>(
            logger,
            """
            Compile the supplied RPG story into one directed acyclic graph of named quest blocks. Every
            block has Id, BlockType, NodeCount, and DependsOnBlockIds. Id is lowercase kebab-case.
            Dependencies name earlier blocks whose terminal
            quests must precede this block.

            Types: IncitingLead (1); Investigation (1-3); Escalation (1-2); Reversal (1-2); Favor (1);
            ParallelThreads (2 required branches); ExclusiveApproach (2 alternative branches, each
            side exactly one node — a quick fork that reconverges immediately); ExclusiveBranch
            (5-12 nodes across 2-4 alternative routes of different lengths — routes that diverge for
            several quests before reconverging, for a choice that should feel consequential, not
            cosmetic); FactDisclosure (2: fact request plus support); Finale or EpilogueHook
            (1 terminal).

            Treat the requested node budget as a target, not just a ceiling: use most of it rather
            than stopping as soon as the story's central conflict is technically resolved. Padding
            with filler is still wrong — if the story doesn't obviously fill the budget, deepen
            existing threads with additional Investigation, Escalation, Reversal, or FactDisclosure
            steps rather than inventing an unrelated subplot. Exactly one terminal block is required.
            Preserve every major story turn. Include ParallelThreads, ExclusiveApproach, and
            FactDisclosure when the story permits. For a node budget of 16 or more, include at least
            one ExclusiveBranch — a chain built only from single-node forks and a straight spine
            reads as shallow regardless of how much prose surrounds it.

            Don't save every structurally interesting block for right before the terminal block —
            spread real complexity across the whole graph. A branch, reversal, or fact disclosure
            placed early or in the middle, whose outcome then shapes what's available later, reads
            as more consequential than a pile of choices saved for the very end. The required
            ExclusiveBranch does not have to be the last major block before Finale or EpilogueHook.
            Respond with raw JSON only.
            """,
            $"Story: {story.Summary}\n{string.Join("\n", story.Chapters.Select((chapter, index) => $"Chapter {index + 1}: {chapter.Description}\n{string.Join("\n", chapter.Turns.Select(turn => $"- {turn}"))}"))}\nNode budget: {nodeBudget}",
            graph => Validate(graph, nodeBudget),
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 16384 }
        );
        return new QuestChainBlockGraph(
            schema
                .Blocks.Select(block => new QuestChainBlockGraphBlock(
                    block.Id,
                    Enum.Parse<QuestChainBlockType>(block.BlockType),
                    block.NodeCount,
                    block.DependsOnBlockIds.ToArray()
                ))
                .ToArray()
        );
    }

    private static string? Validate(QuestChainBlockGraphSchema graph, int nodeBudget)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var block in graph.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Id) || !ids.Add(block.Id))
                return "Every block needs a unique Id.";
            if (!Enum.TryParse<QuestChainBlockType>(block.BlockType, out var type))
                return $"Unknown block type \"{block.BlockType}\".";
            var definition = QuestChainBlockCatalog.Get(type);
            if (
                block.NodeCount < definition.MinimumNodeCount
                || block.NodeCount > definition.MaximumNodeCount
            )
                return $"{block.Id} has an invalid node count.";
            total += block.NodeCount;
            if (
                block.DependsOnBlockIds.Any(dependency =>
                    dependency == block.Id || !ids.Contains(dependency)
                )
            )
                return "Every dependency must name an earlier block in this graph.";
        }
        if (total == 0)
            return "The graph must contain at least one node.";
        if (total > nodeBudget)
            return $"Blocks use {total} nodes, which exceeds the maximum budget of {nodeBudget}.";

        var minimumTotal = Math.Max(2, (int)(nodeBudget * MinimumBudgetUsage));
        if (total < minimumTotal)
            return $"Blocks use only {total} of the {nodeBudget}-node budget; use at least {minimumTotal} instead of stopping early.";

        if (
            graph.Blocks.Count(block =>
                block.BlockType
                    is nameof(QuestChainBlockType.Finale)
                        or nameof(QuestChainBlockType.EpilogueHook)
            ) != 1
        )
            return "Use exactly one Finale or EpilogueHook.";

        if (
            nodeBudget >= MinimumBudgetForExclusiveBranch
            && graph.Blocks.All(block =>
                block.BlockType != nameof(QuestChainBlockType.ExclusiveBranch)
            )
        )
            return $"A node budget of {MinimumBudgetForExclusiveBranch} or more must include at least one ExclusiveBranch block.";

        return null;
    }
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
            terminalNodeIdsByBlockId[block.Id] = rebased
                .Where(node =>
                    !rebased
                        .SelectMany(candidate => candidate.PrerequisiteNodeIds)
                        .Contains(node.NodeId)
                )
                .Select(node => node.NodeId)
                .ToArray();
            groupOffset += localNodes.Max(node =>
                Math.Max(node.GroupIndex ?? 0, node.PrerequisiteAlternativeGroupIndex ?? 0)
            );
        }
        return nodes.ToArray();
    }
}
