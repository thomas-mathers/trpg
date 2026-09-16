using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// Fixed vocabulary for QuestChainCandidateEntity.Type — shared between this generator (which
// validates against it) and whichever seed command builds the candidate pool, so both sides agree
// on the discriminator strings without either hard-coding the other's literals.
public static class QuestChainEntityTypes
{
    public const string Creature = "Creature";
    public const string Item = "Item";

    // Split from a single generic "Location" type: a Dungeon has hostiles and supports
    // ClearLocation, a Building doesn't and only supports ExploreLocation — collapsing them back
    // into one type would let the LLM point ClearLocation at a building with nothing to clear.
    public const string Dungeon = "Dungeon";
    public const string Building = "Building";

    public static readonly HashSet<string> ExplorableTypes = [Dungeon, Building];
}

public record QuestChainCandidateEntity(Guid Id, string Name, string Type);

public class QuestChainGeneratorInput
{
    public required string ChainPremise { get; init; }
    public required int ChainLength { get; init; }
    public required IReadOnlyList<QuestChainCandidateEntity> AvailableEntities { get; init; }
}

// The 10 real TRPG.Domain.Models.QuestObjective subtypes, as the literal strings the model must
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
    SpeakToCreature,
    CollectItem,
    GiveItems,
    GiveItemKind,
    DeliverItem,
}

internal class QuestChainSchema
{
    public List<QuestChainNodeSchema> Nodes { get; init; } = [];
}

internal class QuestChainNodeSchema
{
    public string NodeId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string? GiverEntityId { get; init; }
    public List<string> PrerequisiteNodeIds { get; init; } = [];
    public List<QuestChainObjectiveSchema> Objectives { get; init; } = [];
}

internal class QuestChainObjectiveSchema
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string ObjectiveType { get; init; } = "";
    public string? TargetEntityId { get; init; }
    public string? RecipientEntityId { get; init; }
    public string? ItemNameForKind { get; init; }
    public string? CreatureTypeCategory { get; init; }
    public int RequiredAmount { get; init; } = 1;
}

// Public result shape: NodeId stays a local string label (the caller resolves it to a real Guid
// per node when persisting), everything else is fully typed/parsed — the internal wire schema
// above never crosses the assembly boundary.
public record QuestChainGeneratedNode(
    string NodeId,
    string Name,
    string Description,
    Guid GiverEntityId,
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
    CreatureType? CreatureTypeCategory,
    int RequiredAmount
);

public class QuestChainGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainGenerator> logger
)
{
    private static readonly IReadOnlySet<string> ValidObjectiveTypes =
        Enum.GetNames<GeneratedObjectiveType>().ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlyList<QuestChainGeneratedNode>> Generate(
        QuestChainGeneratorInput input,
        CancellationToken cancellationToken
    )
    {
        var entityTypesById = input.AvailableEntities.ToDictionary(
            entity => entity.Id.ToString(),
            entity => entity.Type,
            StringComparer.Ordinal
        );
        var entityList = string.Join(
            "\n",
            input.AvailableEntities.Select(entity =>
                $"- id={entity.Id}, name=\"{entity.Name}\", type={entity.Type}"
            )
        );

        var systemPrompt = $"""
            You are authoring an entire procedurally generated quest chain in one pass for a text
            RPG, as a directed acyclic graph (DAG) of quest nodes rather than a straight line.

            Rules:
            - You may only reference entities from the provided list for GiverEntityId and any
              TargetEntityId/RecipientEntityId. Never invent an entity id.
            - Every node MUST have a non-null GiverEntityId, and it must be a "{QuestChainEntityTypes.Creature}"-type
              entity — pick the one that most plausibly gives this quest, even if the fit is loose.
              A quest with no giver can never be offered to the player.
            - Every objective needs a TargetEntityId, EXCEPT KillCreatureType (uses
              CreatureTypeCategory instead) and GiveItemKind (uses ItemNameForKind instead). If
              nothing in the entity list plausibly fits an objective you want to write, use one of
              those two category-based types instead of leaving TargetEntityId null.
            - TargetEntityId's type must match the objective type: KillCreature/FreeCreature/
              SpeakToCreature need a "{QuestChainEntityTypes.Creature}"-type entity; ClearLocation
              needs a "{QuestChainEntityTypes.Dungeon}"-type entity specifically (only dungeons have
              hostiles to clear); ExploreLocation accepts either a "{QuestChainEntityTypes.Dungeon}"-
              or "{QuestChainEntityTypes.Building}"-type entity; CollectItem/GiveItems/DeliverItem
              need an "{QuestChainEntityTypes.Item}"-type entity.
            - GiveItems and DeliverItem also need a non-null RecipientEntityId (who receives the
              item, a "{QuestChainEntityTypes.Creature}"-type entity); GiveItemKind always needs one
              too.
            - Give every node a unique NodeId of the form "node-1", "node-2", etc. These are local
              labels for this chain only, not real entity ids — use them only in
              PrerequisiteNodeIds to wire up the graph.
            - A node with an empty PrerequisiteNodeIds list is available from the very start.
            - Use branching: give at least one node 2-3 sibling nodes that share the same single
              prerequisite, representing mutually exclusive paths the player can choose between
              (completing one forecloses the others).
            - Use convergence at least once: a node may list more than one PrerequisiteNodeIds
              entry, meaning it only unlocks once every one of those nodes is complete.
            - The graph must be acyclic — no node may (transitively) require itself.
            - Produce approximately {input.ChainLength} nodes total across the whole graph.
            - Every Name field (on each node and each objective) must be a short quest-log title,
              3-6 words, distinct from the longer Description field. Never leave Name blank.

            Every objective must be one of these exact ObjectiveType values — the game can only
            mechanically enforce these, nothing else, so do not invent a different kind of
            objective even if it would fit the story better:
            - KillCreature: kill one specific creature. TargetEntityId = that creature's id.
            - KillCreatureType: kill any creatures of a category. CreatureTypeCategory must be
              exactly one of: {string.Join(", ", Enum.GetNames<CreatureType>())}. RequiredAmount =
              how many to kill.
            - FreeCreature: free one specific captured/restrained creature. TargetEntityId = that
              creature's id.
            - ClearLocation: defeat every hostile in one specific dungeon. TargetEntityId = that
              dungeon's id.
            - ExploreLocation: simply reach/enter one specific dungeon or building. TargetEntityId
              = that dungeon's or building's id.
            - SpeakToCreature: start a conversation with one specific creature. TargetEntityId =
              that creature's id. (This completes the instant the conversation opens — it cannot
              gate on what gets said or learned.)
            - CollectItem: acquire one specific existing item, no delivery required. TargetEntityId
              = that item's id.
            - GiveItems: acquire one or more specific existing items and hand them to a recipient.
              TargetEntityId = the item's id. RecipientEntityId = who to give it to.
            - GiveItemKind: acquire RequiredAmount items that share a fungible kind/name (not a
              specific existing item id — this is for a kind of thing that doesn't exist yet, like
              a monster drop) and hand them to a recipient. ItemNameForKind = the kind's name.
              RecipientEntityId = who to give it to.
            - DeliverItem: carry one specific existing item (that the player already has or will
              acquire) to a recipient. TargetEntityId = the item's id. RecipientEntityId = who to
              deliver it to.
            If a narrative beat you want to write doesn't fit any of these mechanics, either drop
            it or reshape it into one that does — do not leave ObjectiveType blank or invent a new
            value.
            - Respond with only raw JSON matching the schema. No markdown, no commentary.
            """;

        var userPrompt = $"""
            Chain premise:
            {input.ChainPremise}

            Available entities:
            {entityList}
            """;

        var schema = await client.GetValidatedJson<QuestChainSchema>(
            logger,
            systemPrompt,
            userPrompt,
            schema => Validate(schema, entityTypesById),
            cancellationToken
        );

        return schema
            .Nodes.Select(node => new QuestChainGeneratedNode(
                node.NodeId,
                node.Name,
                node.Description,
                Guid.Parse(node.GiverEntityId!),
                node.PrerequisiteNodeIds,
                node.Objectives.Select(objective => new QuestChainGeneratedObjective(
                        objective.Name,
                        objective.Description,
                        Enum.Parse<GeneratedObjectiveType>(objective.ObjectiveType),
                        objective.TargetEntityId is { } targetId ? Guid.Parse(targetId) : null,
                        objective.RecipientEntityId is { } recipientId
                            ? Guid.Parse(recipientId)
                            : null,
                        objective.ItemNameForKind,
                        objective.CreatureTypeCategory is { } category
                            ? Enum.Parse<CreatureType>(category, true)
                            : null,
                        objective.RequiredAmount
                    ))
                    .ToArray()
            ))
            .ToArray();
    }

    internal static string? Validate(
        QuestChainSchema schema,
        IReadOnlyDictionary<string, string> entityTypesById
    )
    {
        if (schema.Nodes.Count == 0)
        {
            return "The chain must contain at least one node.";
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in schema.Nodes)
        {
            if (!nodeIds.Add(node.NodeId))
            {
                return $"Duplicate NodeId \"{node.NodeId}\" — every NodeId must be unique.";
            }
        }

        foreach (var node in schema.Nodes)
        {
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

            var objectiveError = ValidateObjectives(node, entityTypesById);
            if (objectiveError != null)
            {
                return objectiveError;
            }
        }

        var cycleError = DetectCycle(schema.Nodes);
        if (cycleError != null)
        {
            return cycleError;
        }

        return null;
    }

    internal static string? ValidateObjectives(
        QuestChainNodeSchema node,
        IReadOnlyDictionary<string, string> entityTypesById
    )
    {
        foreach (var objective in node.Objectives)
        {
            var label = $"Objective \"{objective.Name}\" on node \"{node.NodeId}\"";

            if (string.IsNullOrWhiteSpace(objective.Name))
            {
                return $"An objective on node \"{node.NodeId}\" has a blank Name.";
            }

            if (!ValidObjectiveTypes.Contains(objective.ObjectiveType))
            {
                return $"{label} has invalid ObjectiveType \"{objective.ObjectiveType}\" — it must be one of: {string.Join(", ", ValidObjectiveTypes)}.";
            }

            if (objective.RequiredAmount < 1)
            {
                return $"{label} has RequiredAmount {objective.RequiredAmount}, which must be at least 1.";
            }

            var requiredFieldError = ValidateRequiredFieldsForType(
                node,
                objective,
                entityTypesById,
                label
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
        string label
    )
    {
        var type = Enum.Parse<GeneratedObjectiveType>(objective.ObjectiveType);

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

        var expectedTargetTypes = type switch
        {
            GeneratedObjectiveType.KillCreature
            or GeneratedObjectiveType.FreeCreature
            or GeneratedObjectiveType.SpeakToCreature => [QuestChainEntityTypes.Creature],
            GeneratedObjectiveType.ClearLocation => [QuestChainEntityTypes.Dungeon],
            GeneratedObjectiveType.ExploreLocation => QuestChainEntityTypes.ExplorableTypes,
            GeneratedObjectiveType.CollectItem
            or GeneratedObjectiveType.GiveItems
            or GeneratedObjectiveType.DeliverItem => [QuestChainEntityTypes.Item],
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
