namespace TRPG.Application.WorldGeneration.Generators;

public enum QuestChainBlockType
{
    IncitingLead,
    Investigation,
    FactDisclosure,
    QuickBottleneck,
    BranchAndBottleneck,
    Escalation,
    Reversal,
    Favor,
    FloatingModules,
    SideQuest,
    Finale,
    EpilogueHook,
}

// Groups block types by how the composer treats them: Linear/Shape compete for a spot on the
// spine, Decoration is placed afterward and never gates anything, Start/Terminal open and close
// the chain.
public enum QuestChainBlockKind
{
    Start,
    Linear,
    Shape,
    Decoration,
    Terminal,
}

public record QuestChainBlockDefinition(
    QuestChainBlockType Type,
    int MinimumNodeCount,
    int MaximumNodeCount,
    QuestChainBlockKind Kind
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
            QuestChainBlockKind.Start
        ),
        [QuestChainBlockType.Investigation] = new(
            QuestChainBlockType.Investigation,
            MinimumNodeCount: 1,
            MaximumNodeCount: 3,
            QuestChainBlockKind.Linear
        ),
        [QuestChainBlockType.FactDisclosure] = new(
            QuestChainBlockType.FactDisclosure,
            MinimumNodeCount: 2,
            MaximumNodeCount: 2,
            QuestChainBlockKind.Linear
        ),
        [QuestChainBlockType.QuickBottleneck] = new(
            QuestChainBlockType.QuickBottleneck,
            MinimumNodeCount: 2,
            MaximumNodeCount: 2,
            QuestChainBlockKind.Shape
        ),
        [QuestChainBlockType.BranchAndBottleneck] = new(
            QuestChainBlockType.BranchAndBottleneck,
            MinimumNodeCount: 5,
            MaximumNodeCount: 12,
            QuestChainBlockKind.Shape
        ),
        [QuestChainBlockType.Escalation] = new(
            QuestChainBlockType.Escalation,
            MinimumNodeCount: 1,
            MaximumNodeCount: 2,
            QuestChainBlockKind.Linear
        ),
        [QuestChainBlockType.Reversal] = new(
            QuestChainBlockType.Reversal,
            MinimumNodeCount: 1,
            MaximumNodeCount: 2,
            QuestChainBlockKind.Linear
        ),
        [QuestChainBlockType.Favor] = new(
            QuestChainBlockType.Favor,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            QuestChainBlockKind.Linear
        ),
        [QuestChainBlockType.FloatingModules] = new(
            QuestChainBlockType.FloatingModules,
            MinimumNodeCount: 4,
            MaximumNodeCount: 9,
            QuestChainBlockKind.Shape
        ),
        [QuestChainBlockType.SideQuest] = new(
            QuestChainBlockType.SideQuest,
            MinimumNodeCount: 1,
            MaximumNodeCount: 2,
            QuestChainBlockKind.Decoration
        ),
        [QuestChainBlockType.Finale] = new(
            QuestChainBlockType.Finale,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            QuestChainBlockKind.Terminal
        ),
        [QuestChainBlockType.EpilogueHook] = new(
            QuestChainBlockType.EpilogueHook,
            MinimumNodeCount: 1,
            MaximumNodeCount: 1,
            QuestChainBlockKind.Terminal
        ),
    };

    public static QuestChainBlockDefinition Get(QuestChainBlockType type) => Definitions[type];

    public static IReadOnlyCollection<QuestChainBlockDefinition> All =>
        Definitions.Values.ToArray();
}
