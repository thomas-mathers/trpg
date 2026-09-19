namespace TRPG.Application.WorldGeneration.Generators;

public enum QuestChainBlockGenerationScope
{
    ContinuesChain,
    ConcludesChain,
}

public record QuestChainBlockSelection(QuestChainBlockType Type, int NodeCount);

public static class QuestChainBlockSequenceValidator
{
    public static string? Validate(
        IReadOnlyList<QuestChainBlockSelection> selections,
        int nodeBudget,
        QuestChainBlockGenerationScope scope = QuestChainBlockGenerationScope.ConcludesChain
    )
    {
        if (nodeBudget < 1)
        {
            return "The node budget must be at least one.";
        }

        var openThreadCount = 0;
        var usedNodeCount = 0;
        for (var index = 0; index < selections.Count; index++)
        {
            var selection = selections[index];
            var definition = QuestChainBlockCatalog.Get(selection.Type);
            if (
                selection.NodeCount < definition.MinimumNodeCount
                || selection.NodeCount > definition.MaximumNodeCount
            )
            {
                return $"{selection.Type} must use {FormatNodeCount(definition)}.";
            }

            if (
                scope == QuestChainBlockGenerationScope.ContinuesChain
                && IsTerminal(selection.Type)
            )
            {
                return $"{selection.Type} can only end a quest chain.";
            }

            var hasRequiredOpenThreads = IsTerminal(selection.Type)
                ? openThreadCount > 0
                : openThreadCount == definition.RequiredOpenThreadCount;
            if (!hasRequiredOpenThreads)
            {
                return IsTerminal(selection.Type)
                    ? $"{selection.Type} requires at least one open thread."
                    : $"{selection.Type} requires exactly {definition.RequiredOpenThreadCount} open thread(s), but has {openThreadCount}.";
            }

            usedNodeCount += selection.NodeCount;
            if (usedNodeCount > nodeBudget)
            {
                return $"The selected blocks use {usedNodeCount} nodes, exceeding the {nodeBudget}-node budget.";
            }

            openThreadCount = definition.ResultingOpenThreadCount;
            if (IsTerminal(selection.Type) && index != selections.Count - 1)
            {
                return $"{selection.Type} must be the last block.";
            }

            var remainingNodeBudget = nodeBudget - usedNodeCount;
            if (
                scope == QuestChainBlockGenerationScope.ConcludesChain
                && openThreadCount > 0
                && remainingNodeBudget == 0
            )
            {
                return $"{selection.Type} leaves an open thread with no remaining node budget for a terminal block.";
            }
        }

        if (usedNodeCount != nodeBudget)
        {
            return $"The selected blocks use {usedNodeCount} nodes, but the budget is {nodeBudget}.";
        }

        return scope switch
        {
            QuestChainBlockGenerationScope.ConcludesChain when openThreadCount == 0 => null,
            QuestChainBlockGenerationScope.ConcludesChain =>
                "The selected blocks leave an open thread; add a Finale or EpilogueHook to close it.",
            QuestChainBlockGenerationScope.ContinuesChain when openThreadCount > 0 => null,
            QuestChainBlockGenerationScope.ContinuesChain =>
                "The selected blocks close every thread; a continuing chapter must leave one open.",
            _ => throw new ArgumentOutOfRangeException(nameof(scope)),
        };
    }

    private static bool IsTerminal(QuestChainBlockType type) =>
        type is QuestChainBlockType.Finale or QuestChainBlockType.EpilogueHook;

    private static string FormatNodeCount(QuestChainBlockDefinition definition) =>
        definition.MinimumNodeCount == definition.MaximumNodeCount
            ? $"exactly {definition.MinimumNodeCount} node(s)"
            : $"between {definition.MinimumNodeCount} and {definition.MaximumNodeCount} nodes";
}
