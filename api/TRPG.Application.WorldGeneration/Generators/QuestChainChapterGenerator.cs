namespace TRPG.Application.WorldGeneration.Generators;

public class QuestChainChapterGenerator(QuestChainBlockBasedGenerator blockBasedGenerator)
{
    private const int ChapterNodeCount = 8;

    public async Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        CancellationToken cancellationToken = default
    )
    {
        if (input.ChainLength % ChapterNodeCount != 0)
        {
            throw new InvalidOperationException(
                $"Chapter generation requires a chain length divisible by {ChapterNodeCount}."
            );
        }

        var chapterCount = input.ChainLength / ChapterNodeCount;
        var chapters = new List<QuestChainGeneratedResult>();
        IReadOnlyList<string> frontierNodeIds = [];
        IReadOnlyList<QuestChainGeneratedNode> frontierNodes = [];
        IReadOnlyList<QuestChainGeneratedFact> establishedFacts = [];
        for (var chapterIndex = 1; chapterIndex <= chapterCount; chapterIndex++)
        {
            var scope =
                chapterIndex == chapterCount
                    ? QuestChainBlockGenerationScope.ConcludesChain
                    : QuestChainBlockGenerationScope.ContinuesChain;
            var chapter = await blockBasedGenerator.Generate(
                new QuestChainGeneratorInput
                {
                    ChainPremise = BuildChapterPremise(
                        input.ChainPremise,
                        chapterIndex,
                        scope,
                        frontierNodes,
                        establishedFacts
                    ),
                    ChainLength = ChapterNodeCount,
                    AvailableEntities = input.AvailableEntities,
                },
                scope,
                cancellationToken
            );
            var rebasedChapter = QuestChainChapterStitcher.Rebase(
                chapter,
                chapterIndex,
                frontierNodeIds
            );
            chapters.Add(rebasedChapter);
            frontierNodeIds = QuestChainChapterStitcher.GetTerminalNodeIds(rebasedChapter);
            frontierNodes = rebasedChapter
                .Nodes.Where(node => frontierNodeIds.Contains(node.NodeId, StringComparer.Ordinal))
                .ToArray();
            establishedFacts = chapters.SelectMany(chapter => chapter.Facts).ToArray();
        }

        return new QuestChainGeneratedResult(
            chapters.SelectMany(chapter => chapter.Facts).ToArray(),
            chapters.SelectMany(chapter => chapter.Nodes).ToArray()
        );
    }

    private static string BuildChapterPremise(
        string chainPremise,
        int chapterIndex,
        QuestChainBlockGenerationScope scope,
        IReadOnlyList<QuestChainGeneratedNode> frontierNodes,
        IReadOnlyList<QuestChainGeneratedFact> establishedFacts
    ) =>
        frontierNodes.Count == 0
            ? $"Chapter {chapterIndex}: {chainPremise}"
            : $"""
                Chapter {chapterIndex}: Continue this story: {chainPremise}

                The player has completed the prior chapter's frontier quest(s):
                {string.Join(
                    "\n",
                    frontierNodes.Select(node => $"- {node.Name}: {node.Description}")
                )}

                Established facts from the prior chapter:
                {string.Join("\n", establishedFacts.Select(fact => $"- {fact.Subject}: {fact.Value}"))}

                {(scope == QuestChainBlockGenerationScope.ConcludesChain
                    ? "Bring the central threat to a satisfying conclusion."
                    : "Move the central threat forward, but leave it unresolved for the next chapter.")}
                Do not present an established fact as a new discovery and do not contradict it.
                """;
}

public static class QuestChainChapterStitcher
{
    public static QuestChainGeneratedResult Rebase(
        QuestChainGeneratedResult chapter,
        int chapterIndex,
        IReadOnlyList<string> entryNodeIds
    )
    {
        var nodeIds = chapter.Nodes.ToDictionary(
            node => node.NodeId,
            node => $"node-{chapterIndex}-{node.NodeId["node-".Length..]}"
        );
        var factKeys = chapter.Facts.ToDictionary(
            fact => fact.Key,
            fact => $"chapter-{chapterIndex}-{fact.Key}"
        );

        return new QuestChainGeneratedResult(
            chapter.Facts.Select(fact => fact with { Key = factKeys[fact.Key] }).ToArray(),
            chapter
                .Nodes.Select(node => new QuestChainGeneratedNode(
                    nodeIds[node.NodeId],
                    node.Name,
                    node.Description,
                    node.GiverEntityId,
                    RemapFactKey(node.RequiredFactKey, factKeys),
                    node.GroupIndex is { } groupIndex ? chapterIndex * 1000 + groupIndex : null,
                    node.PrerequisiteNodeIds.Count == 0
                        ? entryNodeIds
                        : node.PrerequisiteNodeIds.Select(id => nodeIds[id]).ToArray(),
                    node.Objectives.Select(objective => new QuestChainGeneratedObjective(
                            objective.Name,
                            objective.Description,
                            objective.ObjectiveType,
                            objective.TargetEntityId,
                            objective.RecipientEntityId,
                            objective.ItemNameForKind,
                            objective.NewItemName,
                            objective.CreatureTypeCategory,
                            objective.RequiredAmount,
                            RemapFactKey(objective.FactKey, factKeys),
                            RemapFactKey(objective.ReasonFactKey, factKeys),
                            objective.BaseWillingness,
                            objective.BribeWillingness,
                            objective.IntimidationWillingness,
                            objective
                                .RequiredSupportingQuestNodeIds.Select(id => nodeIds[id])
                                .ToArray(),
                            objective
                                .WeightedSupportingQuestNodeIds.Select(support =>
                                    support with
                                    {
                                        NodeId = nodeIds[support.NodeId],
                                    }
                                )
                                .ToArray()
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
    }

    public static IReadOnlyList<string> GetTerminalNodeIds(QuestChainGeneratedResult chapter)
    {
        var prerequisiteNodeIds = chapter
            .Nodes.SelectMany(node => node.PrerequisiteNodeIds)
            .ToHashSet(StringComparer.Ordinal);
        return chapter
            .Nodes.Where(node => !prerequisiteNodeIds.Contains(node.NodeId))
            .Select(node => node.NodeId)
            .ToArray();
    }

    private static string? RemapFactKey(
        string? factKey,
        IReadOnlyDictionary<string, string> factKeys
    ) => factKey is null ? null : factKeys[factKey];
}
