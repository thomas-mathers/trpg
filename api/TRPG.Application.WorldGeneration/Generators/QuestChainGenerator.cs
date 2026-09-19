using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// Fixed vocabulary for QuestChainCandidateEntity.Type — shared between this generator (which
// validates against it) and whichever seed command builds the candidate pool, so both sides agree
// on the discriminator strings without either hard-coding the other's literals.
public static class QuestChainEntityTypes
{
    public const string Creature = "Creature";

    // Split from a single generic "Location" type: a Dungeon has hostiles and supports
    // ClearLocation, a Building doesn't and only supports ExploreLocation — collapsing them back
    // into one type would let the LLM point ClearLocation at a building with nothing to clear.
    public const string Dungeon = "Dungeon";
    public const string Building = "Building";

    public static readonly HashSet<string> ExplorableTypes = [Dungeon, Building];
}

public record QuestChainCandidateEntity(Guid Id, string Name, string Type, string Description = "");

public class QuestChainGeneratorInput
{
    public required string ChainPremise { get; init; }
    public required int ChainLength { get; init; }
    public required IReadOnlyList<QuestChainCandidateEntity> AvailableEntities { get; init; }
}

// The real TRPG.Domain.Models.QuestObjective subtypes, as the literal strings the model must
// use on the wire. Kept as validated strings rather than a serialized C# enum: AIJsonUtilities'
// default options (used for both schema generation and the markdown-fence fallback parse) have no
// JsonStringEnumConverter, so a raw enum field would round-trip as an opaque integer instead of a
// descriptive label. Validate() rejects anything outside this set and GetValidatedJson retries.
public enum GeneratedObjectiveType
{
    KillCreature,
    KillCreatureType,
    FreeCreature,
    ClearLocation,
    ExploreLocation,
    LearnFactFromCreature,
    ReportFactToCreature,
    CollectItem,
    GiveItems,
    GiveItemKind,
    DeliverItem,
}

internal class QuestChainSchema
{
    public List<QuestChainFactSchema> Facts { get; init; } = [];
    public List<QuestChainNodeSchema> Nodes { get; init; } = [];
}

internal class QuestChainFactSchema
{
    public string Key { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Value { get; init; } = "";
}

internal class QuestChainNodeSchema
{
    public string NodeId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string? GiverEntityId { get; init; }
    public string? RequiredFactKey { get; init; }
    public int? GroupIndex { get; init; }
    public int? PrerequisiteAlternativeGroupIndex { get; init; }
    public List<string> PrerequisiteNodeIds { get; init; } = [];
    public List<QuestChainObjectiveSchema> Objectives { get; init; } = [];
}

internal class QuestChainSupportingQuestSchema
{
    public string NodeId { get; init; } = "";
    public int Weight { get; init; }
}

internal class QuestChainObjectiveSchema
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string ObjectiveType { get; init; } = "";
    public string? TargetEntityId { get; init; }
    public string? RecipientEntityId { get; init; }
    public string? ItemNameForKind { get; init; }
    public string? NewItemName { get; init; }
    public string? CreatureTypeCategory { get; init; }
    public int? RequiredAmount { get; init; }
    public string? FactKey { get; init; }
    public string? ReasonFactKey { get; init; }
    public int? BaseWillingness { get; init; }
    public int? BribeWillingness { get; init; }
    public int? IntimidationWillingness { get; init; }
    public List<string> RequiredSupportingQuestNodeIds { get; init; } = [];
    public List<QuestChainSupportingQuestSchema> WeightedSupportingQuestNodeIds { get; init; } = [];
}

// Public result shape: NodeId stays a local string label (the caller resolves it to a real Guid
// per node when persisting), everything else is fully typed/parsed — the internal wire schema
// above never crosses the assembly boundary.
public record QuestChainGeneratedFact(string Key, string Subject, string Value);

public record QuestChainGeneratedNode(
    string NodeId,
    string Name,
    string Description,
    Guid GiverEntityId,
    string? RequiredFactKey,
    int? GroupIndex,
    int? PrerequisiteAlternativeGroupIndex,
    IReadOnlyList<string> PrerequisiteNodeIds,
    IReadOnlyList<QuestChainGeneratedObjective> Objectives
);

public record QuestChainGeneratedObjective(
    string Name,
    string Description,
    GeneratedObjectiveType ObjectiveType,
    Guid? TargetEntityId,
    Guid? RecipientEntityId,
    string? ItemNameForKind,
    string? NewItemName,
    CreatureType? CreatureTypeCategory,
    int RequiredAmount,
    string? FactKey,
    string? ReasonFactKey,
    int? BaseWillingness,
    int? BribeWillingness,
    int? IntimidationWillingness,
    IReadOnlyList<string> RequiredSupportingQuestNodeIds,
    IReadOnlyList<QuestChainGeneratedSupportingQuest> WeightedSupportingQuestNodeIds
);

public record QuestChainGeneratedSupportingQuest(string NodeId, int Weight);

public record QuestChainGeneratedResult(
    IReadOnlyList<QuestChainGeneratedFact> Facts,
    IReadOnlyList<QuestChainGeneratedNode> Nodes
);

// Schema validation shared by every LLM authoring stage in the treatment-first pipeline
// (QuestChainContentGenerator's per-slice content, QuestChainFactDisclosureRepairer's repair) —
// every stage produces or assembles a QuestChainSchema, so the rules live in one place.
public static class QuestChainGenerator
{
    private static readonly IReadOnlySet<string> ValidObjectiveTypes =
        Enum.GetNames<GeneratedObjectiveType>().ToHashSet(StringComparer.Ordinal);

    internal static string? Validate(
        QuestChainSchema schema,
        IReadOnlyDictionary<string, string> entityTypesById,
        int? expectedNodeCount = null
    )
    {
        var fieldError = ValidateFields(schema, entityTypesById, expectedNodeCount);
        if (fieldError != null)
        {
            return fieldError;
        }

        return ValidateTopology(schema);
    }

    internal static string? ValidateFields(
        QuestChainSchema schema,
        IReadOnlyDictionary<string, string> entityTypesById,
        int? expectedNodeCount = null
    )
    {
        if (schema.Nodes.Count == 0)
        {
            return "The chain must contain at least one node.";
        }

        if (expectedNodeCount is { } count && schema.Nodes.Count != count)
        {
            return $"The chain must contain exactly {count} nodes.";
        }

        var factsError = ValidateFacts(schema.Facts);
        if (factsError != null)
        {
            return factsError;
        }

        var factKeys = schema.Facts.Select(fact => fact.Key).ToHashSet(StringComparer.Ordinal);

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var learnFactCount = 0;
        foreach (var node in schema.Nodes)
        {
            if (
                !node.NodeId.StartsWith("node-", StringComparison.Ordinal)
                || !int.TryParse(node.NodeId[5..], out var nodeNumber)
                || nodeNumber < 1
            )
            {
                return $"NodeId \"{node.NodeId}\" must have the form node-1.";
            }

            if (!nodeIds.Add(node.NodeId))
            {
                return $"Duplicate NodeId \"{node.NodeId}\" — every NodeId must be unique.";
            }
        }

        foreach (var node in schema.Nodes)
        {
            if (
                node.PrerequisiteNodeIds.Distinct(StringComparer.Ordinal).Count()
                != node.PrerequisiteNodeIds.Count
            )
            {
                return $"Node \"{node.NodeId}\" has duplicate prerequisites.";
            }

            foreach (var prerequisiteId in node.PrerequisiteNodeIds)
            {
                if (!nodeIds.Contains(prerequisiteId))
                {
                    return $"Node \"{node.NodeId}\" lists prerequisite \"{prerequisiteId}\" which doesn't exist.";
                }
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                return $"Node \"{node.NodeId}\" has a blank Name.";
            }

            if (string.IsNullOrWhiteSpace(node.Description))
            {
                return $"Node \"{node.NodeId}\" has a blank Description.";
            }

            if (node.RequiredFactKey is { } requiredFactKey && !factKeys.Contains(requiredFactKey))
            {
                return $"Node \"{node.NodeId}\" references unknown RequiredFactKey \"{requiredFactKey}\".";
            }

            var giverError = ValidateEntityReference(
                node.GiverEntityId,
                [QuestChainEntityTypes.Creature],
                entityTypesById,
                $"Node \"{node.NodeId}\"",
                "GiverEntityId",
                required: true
            );
            if (giverError != null)
            {
                return giverError;
            }

            if (node.Objectives.Count == 0)
            {
                return $"Node \"{node.NodeId}\" has no objectives.";
            }

            var objectiveError = ValidateObjectives(
                node,
                entityTypesById,
                factKeys,
                ref learnFactCount
            );
            if (objectiveError != null)
            {
                return objectiveError;
            }
        }

        var supportingQuestError = ValidateSupportingQuestReferences(schema.Nodes, nodeIds);
        if (supportingQuestError != null)
        {
            return supportingQuestError;
        }

        var referencedFactKeys = schema
            .Nodes.SelectMany(node =>
                node.Objectives.SelectMany(objective =>
                    new[] { node.RequiredFactKey, objective.FactKey, objective.ReasonFactKey }
                )
            )
            .Where(key => key != null)
            .ToHashSet(StringComparer.Ordinal);
        if (factKeys.Any(key => !referencedFactKeys.Contains(key)))
        {
            return "Every authored fact must be referenced by a quest node or objective.";
        }

        return null;
    }

    private static string? ValidateTopology(QuestChainSchema schema)
    {
        var exclusiveGroupError = ValidateExclusiveGroups(schema.Nodes);
        if (exclusiveGroupError != null)
        {
            return exclusiveGroupError;
        }

        return DetectCycle(schema.Nodes);
    }

    internal static string? ValidateObjectives(
        QuestChainNodeSchema node,
        IReadOnlyDictionary<string, string> entityTypesById,
        IReadOnlySet<string> factKeys,
        ref int learnFactCount
    )
    {
        foreach (var objective in node.Objectives)
        {
            var label = $"Objective \"{objective.Name}\" on node \"{node.NodeId}\"";

            if (string.IsNullOrWhiteSpace(objective.Name))
            {
                return $"An objective on node \"{node.NodeId}\" has a blank Name.";
            }

            if (string.IsNullOrWhiteSpace(objective.Description))
            {
                return $"{label} has a blank Description.";
            }

            if (!ValidObjectiveTypes.Contains(objective.ObjectiveType))
            {
                return $"{label} has invalid ObjectiveType \"{objective.ObjectiveType}\" — it must be one of: {string.Join(", ", ValidObjectiveTypes)}.";
            }

            if (objective.RequiredAmount is < 1)
            {
                return $"{label} has RequiredAmount {objective.RequiredAmount}, which must be at least 1.";
            }

            var requiredFieldError = ValidateRequiredFieldsForType(
                node,
                objective,
                entityTypesById,
                label,
                factKeys,
                ref learnFactCount
            );
            if (requiredFieldError != null)
            {
                return requiredFieldError;
            }
        }

        return null;
    }

    // Every objective type except KillCreatureType/GiveItemKind identifies its target by a real
    // entity id rather than a free-form category/name, so those types need TargetEntityId of the
    // right kind (and, for the hand-off types, a Creature RecipientEntityId) or they're
    // unconstructable — or worse, silently mis-wired to the wrong kind of entity — at persist time.
    private static string? ValidateRequiredFieldsForType(
        QuestChainNodeSchema node,
        QuestChainObjectiveSchema objective,
        IReadOnlyDictionary<string, string> entityTypesById,
        string label,
        IReadOnlySet<string> factKeys,
        ref int learnFactCount
    )
    {
        var type = Enum.Parse<GeneratedObjectiveType>(objective.ObjectiveType);

        if (type == GeneratedObjectiveType.LearnFactFromCreature)
        {
            return ValidateLearnFactObjective(
                node,
                objective,
                entityTypesById,
                label,
                factKeys,
                ref learnFactCount
            );
        }

        if (type == GeneratedObjectiveType.ReportFactToCreature)
        {
            var reportTargetError = ValidateEntityReference(
                objective.TargetEntityId,
                [QuestChainEntityTypes.Creature],
                entityTypesById,
                label,
                "TargetEntityId",
                required: true
            );
            return reportTargetError
                ?? (
                    objective.FactKey is { } factKey && factKeys.Contains(factKey)
                        ? null
                        : $"{label} references an unknown FactKey."
                );
        }

        if (type == GeneratedObjectiveType.KillCreatureType)
        {
            return Enum.TryParse<CreatureType>(objective.CreatureTypeCategory, true, out _)
                ? null
                : $"{label} is KillCreatureType but has invalid CreatureTypeCategory \"{objective.CreatureTypeCategory}\" — it must be one of: {string.Join(", ", Enum.GetNames<CreatureType>())}.";
        }

        if (type == GeneratedObjectiveType.GiveItemKind)
        {
            if (string.IsNullOrWhiteSpace(objective.ItemNameForKind))
            {
                return $"{label} is GiveItemKind but has no ItemNameForKind.";
            }
            return ValidateEntityReference(
                objective.RecipientEntityId,
                [QuestChainEntityTypes.Creature],
                entityTypesById,
                label,
                "RecipientEntityId",
                required: true
            );
        }

        var mintsNewItem =
            type
            is GeneratedObjectiveType.CollectItem
                or GeneratedObjectiveType.GiveItems
                or GeneratedObjectiveType.DeliverItem;
        if (mintsNewItem && string.IsNullOrWhiteSpace(objective.NewItemName))
        {
            return $"{label} is {objective.ObjectiveType} but has no NewItemName.";
        }

        var expectedTargetTypes = type switch
        {
            GeneratedObjectiveType.KillCreature
            or GeneratedObjectiveType.FreeCreature
            or GeneratedObjectiveType.ReportFactToCreature
            or GeneratedObjectiveType.CollectItem
            or GeneratedObjectiveType.GiveItems
            or GeneratedObjectiveType.DeliverItem => [QuestChainEntityTypes.Creature],
            GeneratedObjectiveType.ClearLocation => [QuestChainEntityTypes.Dungeon],
            GeneratedObjectiveType.ExploreLocation => QuestChainEntityTypes.ExplorableTypes,
            _ => throw new ArgumentOutOfRangeException(nameof(objective)),
        };

        var targetError = ValidateEntityReference(
            objective.TargetEntityId,
            expectedTargetTypes,
            entityTypesById,
            label,
            "TargetEntityId",
            required: true
        );
        if (targetError != null)
        {
            return targetError;
        }

        var requiresRecipient =
            type is GeneratedObjectiveType.GiveItems or GeneratedObjectiveType.DeliverItem;
        return ValidateEntityReference(
            objective.RecipientEntityId,
            [QuestChainEntityTypes.Creature],
            entityTypesById,
            label,
            "RecipientEntityId",
            required: requiresRecipient
        );
    }

    private static string? ValidateEntityReference(
        string? entityId,
        HashSet<string> expectedTypes,
        IReadOnlyDictionary<string, string> entityTypesById,
        string label,
        string fieldName,
        bool required
    )
    {
        if (entityId == null)
        {
            return required ? $"{label} has no {fieldName}." : null;
        }

        if (!entityTypesById.TryGetValue(entityId, out var actualType))
        {
            return $"{label} has {fieldName} \"{entityId}\" which is not in the provided entity list.";
        }

        return expectedTypes.Contains(actualType)
            ? null
            : $"{label} has {fieldName} \"{entityId}\" which is a \"{actualType}\"-type entity, but this field needs one of: {string.Join(", ", expectedTypes)}.";
    }

    private static string? ValidateFacts(IReadOnlyList<QuestChainFactSchema> facts)
    {
        var factKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            if (
                string.IsNullOrWhiteSpace(fact.Key)
                || !fact.Key.All(character =>
                    char.IsLower(character) || char.IsDigit(character) || character == '-'
                )
            )
            {
                return $"Fact key \"{fact.Key}\" must be lowercase kebab-case.";
            }

            if (!factKeys.Add(fact.Key))
            {
                return $"Duplicate fact key \"{fact.Key}\".";
            }

            if (string.IsNullOrWhiteSpace(fact.Subject) || string.IsNullOrWhiteSpace(fact.Value))
            {
                return $"Fact \"{fact.Key}\" must have a nonblank Subject and Value.";
            }
        }

        return null;
    }

    private static string? ValidateLearnFactObjective(
        QuestChainNodeSchema node,
        QuestChainObjectiveSchema objective,
        IReadOnlyDictionary<string, string> entityTypesById,
        string label,
        IReadOnlySet<string> factKeys,
        ref int learnFactCount
    )
    {
        if (++learnFactCount > 1)
        {
            return "A generated chain may contain only one LearnFactFromCreature objective.";
        }

        var targetError = ValidateEntityReference(
            objective.TargetEntityId,
            [QuestChainEntityTypes.Creature],
            entityTypesById,
            label,
            "TargetEntityId",
            required: true
        );
        if (targetError != null)
        {
            return targetError;
        }

        if (objective.FactKey is not { } factKey || !factKeys.Contains(factKey))
        {
            return $"{label} references an unknown FactKey.";
        }

        if (node.RequiredFactKey == factKey)
        {
            return $"{label} cannot require its own primary fact.";
        }

        var willingness = new[]
        {
            objective.BaseWillingness,
            objective.BribeWillingness,
            objective.IntimidationWillingness,
        };
        if (willingness.Any(value => value is null or < 0 or > 100))
        {
            return $"{label} must provide three willingness values from 0 to 100.";
        }

        if (
            objective.ReasonFactKey is { } reasonFactKey
            && (reasonFactKey == factKey || !factKeys.Contains(reasonFactKey))
        )
        {
            return $"{label} has an invalid ReasonFactKey.";
        }

        if (
            objective.ReasonFactKey is null
            && (
                objective.RequiredSupportingQuestNodeIds.Count > 0
                || objective.WeightedSupportingQuestNodeIds.Count > 0
            )
        )
        {
            return $"{label} must provide a ReasonFactKey when it has supporting quests.";
        }

        if (
            objective
                .RequiredSupportingQuestNodeIds.Concat(
                    objective.WeightedSupportingQuestNodeIds.Select(support => support.NodeId)
                )
                .Distinct(StringComparer.Ordinal)
                .Count()
            != objective.RequiredSupportingQuestNodeIds.Count
                + objective.WeightedSupportingQuestNodeIds.Count
        )
        {
            return $"{label} has duplicate supporting quests.";
        }

        if (objective.RequiredSupportingQuestNodeIds.Count != 0)
        {
            return $"{label} cannot have required supporting quests.";
        }

        if (
            objective
                .RequiredSupportingQuestNodeIds.Concat(
                    objective.WeightedSupportingQuestNodeIds.Select(support => support.NodeId)
                )
                .Any(nodeId =>
                    nodeId == node.NodeId || !nodeId.StartsWith("node-", StringComparison.Ordinal)
                )
        )
        {
            return $"{label} has an invalid supporting quest node.";
        }

        if (
            objective.WeightedSupportingQuestNodeIds.Any(support =>
                support.Weight < 30 || support.Weight > 60
            )
        )
        {
            return $"{label} has a weighted supporting quest outside 30 to 60.";
        }

        return null;
    }

    private static string? ValidateSupportingQuestReferences(
        IReadOnlyList<QuestChainNodeSchema> nodes,
        IReadOnlySet<string> nodeIds
    )
    {
        var nodesById = nodes.ToDictionary(node => node.NodeId);
        foreach (var node in nodes)
        {
            foreach (
                var objective in node.Objectives.Where(objective =>
                    objective.ObjectiveType == nameof(GeneratedObjectiveType.LearnFactFromCreature)
                    && objective.ReasonFactKey != null
                )
            )
            {
                var supportNodeIds = objective.RequiredSupportingQuestNodeIds.Concat(
                    objective.WeightedSupportingQuestNodeIds.Select(support => support.NodeId)
                );
                foreach (var supportNodeId in supportNodeIds)
                {
                    if (!nodeIds.Contains(supportNodeId))
                    {
                        return $"LearnFactFromCreature on node \"{node.NodeId}\" references unknown supporting node \"{supportNodeId}\".";
                    }

                    var supportNode = nodes.Single(candidate => candidate.NodeId == supportNodeId);
                    if (supportNode.RequiredFactKey != objective.ReasonFactKey)
                    {
                        return $"Supporting node \"{supportNodeId}\" must require the learn-fact reason.";
                    }

                    if (RequiresNode(node, supportNodeId, nodesById))
                    {
                        return $"LearnFactFromCreature on node \"{node.NodeId}\" must become available before supporting node \"{supportNodeId}\".";
                    }

                    if (RequiresNode(supportNode, node.NodeId, nodesById))
                    {
                        return $"Supporting node \"{supportNodeId}\" must not require completing LearnFactFromCreature node \"{node.NodeId}\" first.";
                    }
                }
            }
        }

        return null;
    }

    private static bool RequiresNode(
        QuestChainNodeSchema node,
        string requiredNodeId,
        IReadOnlyDictionary<string, QuestChainNodeSchema> nodesById
    ) => CollectAncestorNodeIds(node, nodesById).Contains(requiredNodeId);

    private static string? ValidateExclusiveGroups(IReadOnlyList<QuestChainNodeSchema> nodes)
    {
        var nodesById = nodes.ToDictionary(node => node.NodeId);
        foreach (
            var group in nodes
                .Where(node => node.GroupIndex != null)
                .GroupBy(node => node.GroupIndex)
        )
        {
            var groupedNodes = group.ToArray();
            for (var firstIndex = 0; firstIndex < groupedNodes.Length; firstIndex++)
            {
                var firstAncestors = CollectAncestorNodeIds(groupedNodes[firstIndex], nodesById);
                for (
                    var secondIndex = firstIndex + 1;
                    secondIndex < groupedNodes.Length;
                    secondIndex++
                )
                {
                    var secondAncestors = CollectAncestorNodeIds(
                        groupedNodes[secondIndex],
                        nodesById
                    );
                    if (!firstAncestors.Overlaps(secondAncestors))
                    {
                        return $"Nodes \"{groupedNodes[firstIndex].NodeId}\" and \"{groupedNodes[secondIndex].NodeId}\" share GroupIndex {group.Key} but have no common ancestor.";
                    }
                }
            }
        }

        return null;
    }

    private static HashSet<string> CollectAncestorNodeIds(
        QuestChainNodeSchema node,
        IReadOnlyDictionary<string, QuestChainNodeSchema> nodesById
    )
    {
        var ancestors = new HashSet<string>(StringComparer.Ordinal);
        var unvisitedPrerequisiteIds = new Stack<string>(node.PrerequisiteNodeIds);
        while (unvisitedPrerequisiteIds.TryPop(out var prerequisiteNodeId))
        {
            if (
                ancestors.Add(prerequisiteNodeId)
                && nodesById.TryGetValue(prerequisiteNodeId, out var prerequisiteNode)
            )
            {
                foreach (var nestedPrerequisiteId in prerequisiteNode.PrerequisiteNodeIds)
                {
                    unvisitedPrerequisiteIds.Push(nestedPrerequisiteId);
                }
            }
        }

        return ancestors;
    }

    // Kahn's algorithm: repeatedly remove nodes with no remaining incoming-from-unvisited
    // prerequisites. If nodes remain once no more can be removed, they form a cycle.
    internal static string? DetectCycle(IReadOnlyList<QuestChainNodeSchema> nodes)
    {
        var remainingPrerequisiteCountByNodeId = nodes.ToDictionary(
            node => node.NodeId,
            node => node.PrerequisiteNodeIds.Count
        );
        var dependentNodeIdsByPrerequisiteId = nodes
            .SelectMany(node =>
                node.PrerequisiteNodeIds.Select(prerequisiteId => (prerequisiteId, node.NodeId))
            )
            .GroupBy(pair => pair.prerequisiteId, pair => pair.NodeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var ready = new Queue<string>(
            remainingPrerequisiteCountByNodeId
                .Where(pair => pair.Value == 0)
                .Select(pair => pair.Key)
        );
        var visitedCount = 0;

        while (ready.Count > 0)
        {
            var nodeId = ready.Dequeue();
            visitedCount++;

            foreach (
                var dependentNodeId in dependentNodeIdsByPrerequisiteId.GetValueOrDefault(
                    nodeId,
                    []
                )
            )
            {
                if (--remainingPrerequisiteCountByNodeId[dependentNodeId] == 0)
                {
                    ready.Enqueue(dependentNodeId);
                }
            }
        }

        return visitedCount == nodes.Count
            ? null
            : "The chain contains a cycle — some node (transitively) requires itself.";
    }
}
