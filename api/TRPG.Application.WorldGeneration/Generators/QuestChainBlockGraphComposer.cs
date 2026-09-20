namespace TRPG.Application.WorldGeneration.Generators;

public record QuestChainBlockGraph(IReadOnlyList<QuestChainBlockGraphBlock> Blocks);

public record QuestChainBlockGraphBlock(
    string Id,
    QuestChainBlockType BlockType,
    int NodeCount,
    IReadOnlyList<string> DependsOnBlockIds
);

// Replaces the LLM call that used to author the block graph — the graph's shape now comes
// entirely from QuestChainBlockCatalog and a weighted-random walk, so there is nothing left here
// that needs schema validation or a retry loop. Every random decision routes through
// random.NextDouble(), so a test can fully control this class's output by injecting a Random
// subclass that overrides just that one member.
public class QuestChainBlockGraphComposer(Random random)
{
    // Reserving a slice of the full budget for SideQuest decoration up front means the spine
    // never has to guess how much room to leave — whatever isn't spent on the mandatory spine
    // goes to optional content instead.
    private const double SideQuestBudgetShare = 0.18;

    // BranchAndBottleneck is the only block type whose alternative routes diverge for more than
    // one node before reconverging. Without a floor, a chain could go its entire length on
    // single-node forks and a straight spine, which reads as shallow regardless of how much prose
    // surrounds it.
    private const int MinimumBudgetForBranchAndBottleneck = 16;
    private const int MaximumSideQuestsPerChain = 3;
    private const double BranchPlacementProgressThreshold = 0.35;

    private static readonly QuestChainBlockType[] LinearTypes =
    [
        QuestChainBlockType.Investigation,
        QuestChainBlockType.Escalation,
        QuestChainBlockType.Reversal,
        QuestChainBlockType.Favor,
        QuestChainBlockType.FactDisclosure,
    ];

    private static readonly QuestChainBlockType[] ShapeTypes =
    [
        QuestChainBlockType.QuickBottleneck,
        QuestChainBlockType.FloatingModules,
    ];

    // Side quests only ever hang off a plain narrative beat — never the opening lead, a shape
    // block's own output, or the beat right before the terminal.
    private static readonly QuestChainBlockType[] SideQuestAnchorTypes =
    [
        QuestChainBlockType.Investigation,
        QuestChainBlockType.Escalation,
        QuestChainBlockType.Reversal,
        QuestChainBlockType.Favor,
    ];

    private record BlockSpec(QuestChainBlockType Type, int Count);

    public QuestChainBlockGraph Compose(int minimumNodeBudget, int maximumNodeBudget)
    {
        var nodeBudget = RandomInt(minimumNodeBudget, maximumNodeBudget);
        var spine = ComposeSpine(nodeBudget);
        var decorationsByAnchorIndex = AttachSideQuests(spine, nodeBudget);

        var merged = new List<BlockSpec>();
        for (var index = 0; index < spine.Count; index++)
        {
            merged.Add(spine[index]);
            if (decorationsByAnchorIndex.TryGetValue(index, out var sideQuestCounts))
            {
                merged.AddRange(
                    sideQuestCounts.Select(count => new BlockSpec(
                        QuestChainBlockType.SideQuest,
                        count
                    ))
                );
            }
        }

        var blocks = new List<QuestChainBlockGraphBlock>();
        for (var index = 0; index < merged.Count; index++)
        {
            blocks.Add(
                new QuestChainBlockGraphBlock(
                    $"block-{index + 1}",
                    merged[index].Type,
                    merged[index].Count,
                    index == 0 ? [] : [blocks[index - 1].Id]
                )
            );
        }
        return new QuestChainBlockGraph(blocks);
    }

    private List<BlockSpec> ComposeSpine(int nodeBudget)
    {
        var spine = new List<BlockSpec> { new(QuestChainBlockType.IncitingLead, 1) };
        const int terminalCost = 1;

        // Reserve the SideQuest allowance up front, or the spine exactly exhausts the budget
        // and there's nothing left to decorate with. The branch floor still checks the FULL
        // budget — it's a statement about the chain's overall scope, not just its spine.
        var mustPlaceBranch = nodeBudget >= MinimumBudgetForBranchAndBottleneck;
        var spineBudget = nodeBudget - (int)Math.Round(nodeBudget * SideQuestBudgetShare);
        var branchSize = mustPlaceBranch
            ? RandomInt(5, Math.Min(12, Math.Max(5, (spineBudget - 2) / 2)))
            : 0;

        // Reserve the guaranteed branch's budget up front so it can never be crowded out.
        var totalMiddle = spineBudget - 1 - terminalCost - branchSize;
        var middleBudget = totalMiddle;
        var branchPlaced = !mustPlaceBranch;
        var lastWasShape = false;

        while (middleBudget > 0 || !branchPlaced)
        {
            var progress = totalMiddle > 0 ? (double)(totalMiddle - middleBudget) / totalMiddle : 1;

            // Force the guaranteed BranchAndBottleneck in once we're far enough through the
            // budget, same as any other shape block never following one directly.
            if (
                !branchPlaced
                && !lastWasShape
                && (progress >= BranchPlacementProgressThreshold || middleBudget <= 0)
            )
            {
                PlaceBranch(spine, branchSize, ref branchPlaced, ref lastWasShape);
                if (middleBudget <= 0)
                {
                    break;
                }
                continue;
            }
            if (middleBudget <= 0)
            {
                // Last resort regardless of adjacency: the guarantee that it appears at all
                // takes priority over the back-to-back rule once there's no budget left to
                // keep deferring it.
                if (!branchPlaced)
                {
                    PlaceBranch(spine, branchSize, ref branchPlaced, ref lastWasShape);
                }
                break;
            }

            var candidates = BuildCandidates(middleBudget, lastWasShape);
            if (candidates.Count == 0)
            {
                break;
            }

            var selectedType = WeightedPick(candidates);
            var definition = QuestChainBlockCatalog.Get(selectedType);
            var count =
                selectedType == QuestChainBlockType.FactDisclosure
                    ? 2
                    : RandomInt(
                        definition.MinimumNodeCount,
                        Math.Min(definition.MaximumNodeCount, middleBudget)
                    );
            spine.Add(new BlockSpec(selectedType, count));
            middleBudget -= count;
            lastWasShape = definition.Kind == QuestChainBlockKind.Shape;
        }

        spine.Add(new BlockSpec(QuestChainBlockType.Finale, 1));
        return spine;
    }

    private static void PlaceBranch(
        List<BlockSpec> spine,
        int branchSize,
        ref bool branchPlaced,
        ref bool lastWasShape
    )
    {
        spine.Add(new BlockSpec(QuestChainBlockType.BranchAndBottleneck, branchSize));
        branchPlaced = true;
        lastWasShape = true;
    }

    // Never offers two shape blocks back to back — keeps forks from clustering.
    private static List<(int Weight, QuestChainBlockType Type)> BuildCandidates(
        int middleBudget,
        bool lastWasShape
    )
    {
        var candidates = new List<(int Weight, QuestChainBlockType Type)>();
        foreach (var type in LinearTypes)
        {
            if (QuestChainBlockCatalog.Get(type).MinimumNodeCount <= middleBudget)
            {
                candidates.Add((6, type));
            }
        }
        if (!lastWasShape)
        {
            foreach (var type in ShapeTypes)
            {
                if (QuestChainBlockCatalog.Get(type).MinimumNodeCount <= middleBudget)
                {
                    candidates.Add((2, type));
                }
            }
        }
        return candidates;
    }

    private Dictionary<int, List<int>> AttachSideQuests(List<BlockSpec> spine, int nodeBudget)
    {
        var spent = spine.Sum(block => block.Count);
        var leftover = nodeBudget - spent;

        var eligible = new List<int>();
        for (var index = 0; index < spine.Count; index++)
        {
            // Skip the opening beat, the terminal, and the beat right before the terminal.
            if (
                index > 0
                && index < spine.Count - 2
                && SideQuestAnchorTypes.Contains(spine[index].Type)
            )
            {
                eligible.Add(index);
            }
        }
        Shuffle(eligible);

        var decorations = new Dictionary<int, List<int>>();
        var placed = 0;
        foreach (var index in eligible)
        {
            if (placed >= MaximumSideQuestsPerChain || leftover <= 0)
            {
                break;
            }
            var count = Math.Min(RandomInt(1, 2), leftover);
            if (!decorations.TryGetValue(index, out var counts))
            {
                counts = [];
                decorations[index] = counts;
            }
            counts.Add(count);
            leftover -= count;
            placed++;
        }
        return decorations;
    }

    private int RandomInt(int minimumInclusive, int maximumInclusive) =>
        minimumInclusive + (int)(random.NextDouble() * (maximumInclusive - minimumInclusive + 1));

    private T WeightedPick<T>(IReadOnlyList<(int Weight, T Value)> entries)
    {
        var total = entries.Sum(entry => entry.Weight);
        var remaining = random.NextDouble() * total;
        foreach (var (weight, value) in entries)
        {
            if (remaining < weight)
            {
                return value;
            }
            remaining -= weight;
        }
        return entries[^1].Value;
    }

    private void Shuffle(IList<int> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = (int)(random.NextDouble() * (index + 1));
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
