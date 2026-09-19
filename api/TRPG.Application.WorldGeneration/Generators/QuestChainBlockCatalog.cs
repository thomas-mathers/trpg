namespace TRPG.Application.WorldGeneration.Generators;

public enum QuestChainBlockType
{
    IncitingLead,
    Investigation,
    FactDisclosure,
    ExclusiveApproach,
    ExclusiveBranch,
    Escalation,
    Reversal,
    Favor,
    ParallelThreads,
    Finale,
    EpilogueHook,
}

public record QuestChainBlockDefinition(
    QuestChainBlockType Type,
    int MinimumNodeCount,
    int MaximumNodeCount,
    int RequiredOpenThreadCount,
    int ResultingOpenThreadCount
);

public static class QuestChainBlockCatalog
{
    private static readonly IReadOnlyDictionary<
        QuestChainBlockType,
        QuestChainBlockDefinition
    > Definitions = new Dictionary<QuestChainBlockType, QuestChainBlockDefinition>
    {
        [QuestChainBlockType.IncitingLead] = new(
            QuestChainBlockType.IncitingLead,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            RequiredOpenThreadCount: 0,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.Investigation] = new(
            QuestChainBlockType.Investigation,
            MinimumNodeCount: 1,
            MaximumNodeCount: 3,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.FactDisclosure] = new(
            QuestChainBlockType.FactDisclosure,
            MinimumNodeCount: 2,
            MaximumNodeCount: 2,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 2
        ),
        [QuestChainBlockType.ExclusiveApproach] = new(
            QuestChainBlockType.ExclusiveApproach,
            MinimumNodeCount: 2,
            MaximumNodeCount: 2,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.ExclusiveBranch] = new(
            QuestChainBlockType.ExclusiveBranch,
            MinimumNodeCount: 5,
            MaximumNodeCount: 12,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.Escalation] = new(
            QuestChainBlockType.Escalation,
            MinimumNodeCount: 1,
            MaximumNodeCount: 2,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.Reversal] = new(
            QuestChainBlockType.Reversal,
            MinimumNodeCount: 1,
            MaximumNodeCount: 2,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.Favor] = new(
            QuestChainBlockType.Favor,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 1
        ),
        [QuestChainBlockType.ParallelThreads] = new(
            QuestChainBlockType.ParallelThreads,
            MinimumNodeCount: 2,
            MaximumNodeCount: 2,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 2
        ),
        [QuestChainBlockType.Finale] = new(
            QuestChainBlockType.Finale,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 0
        ),
        [QuestChainBlockType.EpilogueHook] = new(
            QuestChainBlockType.EpilogueHook,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            RequiredOpenThreadCount: 1,
            ResultingOpenThreadCount: 0
        ),
    };

    public static QuestChainBlockDefinition Get(QuestChainBlockType type) => Definitions[type];

    public static IReadOnlyCollection<QuestChainBlockDefinition> All =>
        Definitions.Values.ToArray();
}
