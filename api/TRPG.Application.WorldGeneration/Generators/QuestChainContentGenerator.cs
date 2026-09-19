using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal class QuestChainContentSchema
{
    public List<QuestChainContentFactSchema> Facts { get; init; } = [];
    public List<QuestChainContentNodeSchema> Nodes { get; init; } = [];
}

internal class QuestChainContentFactSchema
{
    public string FactKey { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Value { get; init; } = "";
}

internal class QuestChainContentNodeSchema
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string? GiverEntityId { get; init; }
    public string? RequiredFactKey { get; init; }
    public List<QuestChainContentObjectiveSchema> Objectives { get; init; } = [];
}

internal class QuestChainContentObjectiveSchema
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
}

// An entity an earlier content slice already used — either as an objective's target/recipient
// (Role is the objective type, e.g. "FreeCreature") or as a node's giver (Role is "Giver",
// meaning it was last established as free and active). Threaded forward so a later slice (a
// separate LLM call with no visibility into prior slices' output) doesn't independently reuse
// the same entity in a way that contradicts what's already been established — e.g. freeing an
// already-freed captive, or silently treating a recently-active giver as already captured.
public record QuestChainCommittedEntity(
    Guid EntityId,
    string EntityName,
    string Role,
    string QuestName
);

public class QuestChainContentGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainContentGenerator> logger
)
{
    public async Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        IReadOnlyList<QuestChainNodeSkeleton> skeleton,
        QuestChainBlockGenerationScope scope,
        IReadOnlyList<QuestChainCommittedEntity> committedEntities,
        CancellationToken cancellationToken = default
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
                $"- id={entity.Id}, name=\"{entity.Name}\", type={entity.Type}, details=\"{entity.Description}\""
            )
        );
        var committedEntitiesInstruction =
            committedEntities.Count == 0
                ? ""
                : $"""

                Entities already used earlier in this chain:
                {string.Join(
                    "\n",
                    committedEntities.Select(entity =>
                        entity.Role == "Giver"
                            ? $"- {entity.EntityName}: established as free and active, giving \"{entity.QuestName}\""
                            : $"- {entity.EntityName}: {entity.Role} (via \"{entity.QuestName}\")"
                    )
                )}
                Avoid reusing an entity for the same kind of objective unless the story
                specifically revisits it — e.g. don't free someone already freed. An entity
                established as a free, active giver may still change status later (captured,
                killed, turned) if the story calls for it, but that change must be an explicit
                narrated event in the beat that causes it, never silently assumed.
                """;
        var skeletonDescription = string.Join(
            "\n",
            skeleton.Select(node =>
                $"- {node.NodeId}: requires {FormatNodeIds(node.PrerequisiteNodeIds)}; "
                + $"block type {node.BlockType}; "
                + $"exclusive group {node.GroupIndex?.ToString() ?? "none"}; "
                + $"prerequisite alternative group {node.PrerequisiteAlternativeGroupIndex?.ToString() ?? "none"}; "
                + $"fact support {node.FactDisclosureSupportingNodeId ?? "none"}"
            )
        );
        var factDisclosureNodeCount = skeleton.Count(node =>
            node.FactDisclosureSupportingNodeId is not null
        );
        var resolutionInstruction =
            scope == QuestChainBlockGenerationScope.ConcludesChain
                ? "This is the chain conclusion. Resolve the central threat in its terminal block."
                : "This chapter continues a larger chain. Advance or complicate the central threat, but do not resolve it or present any objective as the final confrontation.";
        var systemPrompt = $"""
            You author the narrative and objective content for a fixed text RPG quest graph. The
            graph is read-only context supplied by game code. Write exactly one content node for
            each skeleton node, in its supplied order. Do not output node ids, prerequisites, or
            exclusive groups.

            {resolutionInstruction}

            Shape each node's story beat to its block type: Escalation raises the danger or stakes;
            Reversal changes what the player thought they knew; Favor helps an NPC and earns their
            trust or aid; ParallelThreads pursue distinct leads at the same time; EpilogueHook shows
            consequences and points toward a future story. ExclusiveBranch commits the player to one
            distinct approach; keep every quest in a route pursuing that approach through to its own
            endpoint. Do not put a final confrontation in an EpilogueHook.

            Every content node needs a nonblank 3-6 word Name, concrete Description, and a creature
            GiverEntityId from the entity list. Every objective needs a nonblank 3-6 word Name and
            concrete Description. Use only real entity ids from the supplied list.

            Every objective MUST set ObjectiveType to exactly one of these literal values; never
            leave it blank:
            - KillCreature: TargetEntityId is a creature.
            - KillCreatureType: CreatureTypeCategory is one of {string.Join(
                ", ",
                Enum.GetNames<CreatureType>()
            )} and RequiredAmount is at least one.
            - FreeCreature: TargetEntityId is a creature.
            - ClearLocation: TargetEntityId is a Dungeon.
            - ExploreLocation: TargetEntityId is a Dungeon or Building.
            - LearnFactFromCreature: TargetEntityId is a creature; FactKey and all three willingness
              values are required. ReasonFactKey and a weighted supporting quest are optional.
            - ReportFactToCreature: TargetEntityId is a creature and FactKey is required.
            - CollectItem: TargetEntityId is a creature and NewItemName is required.
            - GiveItems: TargetEntityId and RecipientEntityId are creatures and NewItemName is required.
            - GiveItemKind: ItemNameForKind and creature RecipientEntityId are required.
            - DeliverItem: TargetEntityId and RecipientEntityId are creatures and NewItemName is required.

            The supplied skeleton has {factDisclosureNodeCount} fact-disclosure node(s). When this is
            zero, Facts MUST be [] and LearnFactFromCreature is forbidden. When it is one, author
            exactly one LearnFactFromCreature on that named node. It needs a target creature, FactKey,
            BaseWillingness, BribeWillingness, and IntimidationWillingness from 0 to 100. ReasonFactKey
            and a weighted supporting quest are optional; use them only when the story includes a
            concrete reason for refusal. Game code links the matching support node when the skeleton
            provides one. Facts have lowercase kebab-case FactKey values and nonblank player-facing
            Subject and Value. Every FactKey and ReasonFactKey must have a matching entry in Facts. Copy the exact identifier: if an
            objective has FactKey "cult-symbol", Facts must contain an entry whose FactKey is
            "cult-symbol"; if it has ReasonFactKey "guard-fears-retaliation", Facts must contain an
            entry whose FactKey is "guard-fears-retaliation". Never leave a Facts entry FactKey blank.
            Respond with raw JSON only.
            The JSON root MUST be an object with exactly "facts" and "nodes" array properties;
            never return a bare array.
            """;
        var userPrompt = $"""
            Premise: {input.ChainPremise}

            Fixed skeleton:
            {skeletonDescription}

            Available entities:
            {entityList}
            {committedEntitiesInstruction}
            """;

        var content = await client.GetValidatedJson<QuestChainContentSchema>(
            logger,
            systemPrompt,
            userPrompt,
            schema => Validate(schema, skeleton, entityTypesById),
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 16384 }
        );
        return QuestChainBlockAssembler.Assemble(content, skeleton);
    }

    internal static string? Validate(
        QuestChainContentSchema content,
        IReadOnlyList<QuestChainNodeSkeleton> skeleton,
        IReadOnlyDictionary<string, string> entityTypesById
    )
    {
        if (content.Nodes.Count != skeleton.Count)
        {
            return $"Write exactly {skeleton.Count} content nodes.";
        }

        var schema = QuestChainBlockAssembler.Merge(content, skeleton);
        return QuestChainGenerator.ValidateFields(schema, entityTypesById, skeleton.Count);
    }

    private static string FormatNodeIds(IReadOnlyList<string> nodeIds) =>
        nodeIds.Count == 0 ? "none" : string.Join(", ", nodeIds);
}

internal static class QuestChainBlockAssembler
{
    public static QuestChainGeneratedResult Assemble(
        QuestChainContentSchema content,
        IReadOnlyList<QuestChainNodeSkeleton> skeleton
    )
    {
        var schema = Merge(content, skeleton);
        return new QuestChainGeneratedResult(
            schema
                .Facts.Select(fact => new QuestChainGeneratedFact(
                    fact.Key,
                    fact.Subject,
                    fact.Value
                ))
                .ToArray(),
            schema
                .Nodes.Select(node => new QuestChainGeneratedNode(
                    node.NodeId,
                    node.Name,
                    node.Description,
                    Guid.Parse(node.GiverEntityId!),
                    node.RequiredFactKey,
                    node.GroupIndex,
                    node.PrerequisiteAlternativeGroupIndex,
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
                            objective.NewItemName,
                            objective.CreatureTypeCategory is { } category
                                ? Enum.Parse<CreatureType>(category, true)
                                : null,
                            objective.RequiredAmount ?? 1,
                            objective.FactKey,
                            objective.ReasonFactKey,
                            objective.BaseWillingness,
                            objective.BribeWillingness,
                            objective.IntimidationWillingness,
                            objective.RequiredSupportingQuestNodeIds,
                            objective
                                .WeightedSupportingQuestNodeIds.Select(
                                    support => new QuestChainGeneratedSupportingQuest(
                                        support.NodeId,
                                        support.Weight
                                    )
                                )
                                .ToArray()
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
    }

    internal static QuestChainSchema Merge(
        QuestChainContentSchema content,
        IReadOnlyList<QuestChainNodeSkeleton> skeleton
    )
    {
        var reasonFactKeysBySupportNodeId = content
            .Nodes.Zip(skeleton)
            .SelectMany(pair =>
                pair.First.Objectives.Where(objective =>
                        objective.ObjectiveType
                            == nameof(GeneratedObjectiveType.LearnFactFromCreature)
                        && objective.ReasonFactKey is not null
                        && pair.Second.FactDisclosureSupportingNodeId is not null
                    )
                    .Select(objective => new
                    {
                        SupportNodeId = pair.Second.FactDisclosureSupportingNodeId!,
                        ReasonFactKey = objective.ReasonFactKey!,
                    })
            )
            .ToDictionary(pair => pair.SupportNodeId, pair => pair.ReasonFactKey);

        return new QuestChainSchema
        {
            Facts = content
                .Facts.Select(fact => new QuestChainFactSchema
                {
                    Key = fact.FactKey,
                    Subject = fact.Subject,
                    Value = fact.Value,
                })
                .ToList(),
            Nodes = content
                .Nodes.Zip(skeleton)
                .Select(pair => new QuestChainNodeSchema
                {
                    NodeId = pair.Second.NodeId,
                    Name = pair.First.Name,
                    Description = pair.First.Description,
                    GiverEntityId = pair.First.GiverEntityId,
                    RequiredFactKey = reasonFactKeysBySupportNodeId.TryGetValue(
                        pair.Second.NodeId,
                        out var reasonFactKey
                    )
                        ? reasonFactKey
                        : pair.First.RequiredFactKey,
                    GroupIndex = pair.Second.GroupIndex,
                    PrerequisiteAlternativeGroupIndex =
                        pair.Second.PrerequisiteAlternativeGroupIndex,
                    PrerequisiteNodeIds = pair.Second.PrerequisiteNodeIds.ToList(),
                    Objectives = pair
                        .First.Objectives.Select(objective => new QuestChainObjectiveSchema
                        {
                            Name = objective.Name,
                            Description = objective.Description,
                            ObjectiveType = objective.ObjectiveType,
                            TargetEntityId = objective.TargetEntityId,
                            RecipientEntityId = objective.RecipientEntityId,
                            ItemNameForKind = objective.ItemNameForKind,
                            NewItemName = objective.NewItemName,
                            CreatureTypeCategory = objective.CreatureTypeCategory,
                            RequiredAmount = objective.RequiredAmount,
                            FactKey = objective.FactKey,
                            ReasonFactKey = objective.ReasonFactKey,
                            BaseWillingness = objective.BaseWillingness,
                            BribeWillingness = objective.BribeWillingness,
                            IntimidationWillingness = objective.IntimidationWillingness,
                            RequiredSupportingQuestNodeIds = [],
                            WeightedSupportingQuestNodeIds =
                                objective.ObjectiveType
                                    == nameof(GeneratedObjectiveType.LearnFactFromCreature)
                                && objective.ReasonFactKey is not null
                                && pair.Second.FactDisclosureSupportingNodeId is { } supportNodeId
                                    ?
                                    [
                                        new QuestChainSupportingQuestSchema
                                        {
                                            NodeId = supportNodeId,
                                            Weight = 45,
                                        },
                                    ]
                                    : [],
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }
}
