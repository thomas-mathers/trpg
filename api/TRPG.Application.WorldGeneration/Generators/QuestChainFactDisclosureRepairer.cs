using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal class FactDisclosureRepairSchema
{
    public QuestChainContentFactSchema ReasonFact { get; init; } = new();
    public QuestChainContentNodeSchema SupportQuest { get; init; } = new();
}

public class QuestChainFactDisclosureRepairer(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainFactDisclosureRepairer> logger
)
{
    public async Task<QuestChainGeneratedResult> Repair(
        QuestChainGeneratorInput input,
        QuestChainGeneratedResult generated,
        CancellationToken cancellationToken = default
    )
    {
        var facts = generated.Facts.ToList();
        var nodes = generated.Nodes.ToList();
        var nextNodeNumber = GetNextNodeNumber(nodes);
        foreach (var node in generated.Nodes)
        {
            var objective = node.Objectives.SingleOrDefault(candidate =>
                candidate.ObjectiveType == GeneratedObjectiveType.LearnFactFromCreature
            );
            if (
                objective is null
                || HasSupportingQuest(objective)
                || !BlocksContinuation(node, nodes)
            )
            {
                continue;
            }

            var existingReasonFact = objective.ReasonFactKey is { } reasonFactKey
                ? facts.SingleOrDefault(fact => fact.Key == reasonFactKey)
                : null;
            var primaryFact = facts.Single(fact => fact.Key == objective.FactKey);
            var supportNodeId = $"node-{nextNodeNumber++}";
            var repair = await GenerateRepair(
                input,
                node,
                primaryFact,
                supportNodeId,
                existingReasonFact,
                facts.Select(fact => fact.Key).ToHashSet(StringComparer.Ordinal),
                cancellationToken
            );
            if (existingReasonFact is null)
            {
                facts.Add(repair.ReasonFact);
            }

            var repairedObjective = objective with
            {
                ReasonFactKey = repair.ReasonFact.Key,
                WeightedSupportingQuestNodeIds =
                [
                    new QuestChainGeneratedSupportingQuest(supportNodeId, Weight: 45),
                ],
            };
            var repairedNode = node with
            {
                Objectives = node
                    .Objectives.Select(candidate =>
                        candidate == objective ? repairedObjective : candidate
                    )
                    .ToArray(),
            };
            nodes[nodes.FindIndex(candidate => candidate.NodeId == node.NodeId)] = repairedNode;
            nodes.Add(repair.SupportQuest);
        }

        return new QuestChainGeneratedResult(
            facts.ToArray(),
            nodes.ToArray(),
            generated.GiverFactionId,
            generated.AntagonistFactionId
        );
    }

    private async Task<FactDisclosureRepairResult> GenerateRepair(
        QuestChainGeneratorInput input,
        QuestChainGeneratedNode factNode,
        QuestChainGeneratedFact primaryFact,
        string supportNodeId,
        QuestChainGeneratedFact? existingReasonFact,
        IReadOnlySet<string> existingFactKeys,
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
                $"- id={entity.Id}, name=\"{entity.Name}\", type={entity.Type}, details=\"{entity.Description}\""
            )
        );
        var existingReasonInstruction = existingReasonFact is null
            ? "Author a new reason fact with a unique lowercase kebab-case key."
            : $"Reuse this reason fact exactly: key={existingReasonFact.Key}, subject=\"{existingReasonFact.Subject}\", value=\"{existingReasonFact.Value}\".";
        var systemPrompt = $"""
            Repair one blocking fact-disclosure quest in a text RPG. The player needs the primary fact
            to continue, but the NPC may refuse to share it. Author a reason for withholding and one
            support quest that materially resolves that reason, making later disclosure more likely.

            The support quest must be independently completable before the fact is disclosed. It needs
            a nonblank 3-6 word Name, concrete Description, a creature GiverEntityId from the supplied
            entities, and one or more ordinary objectives. Do not use LearnFactFromCreature for the
            support quest. Use only real entity ids and these objective types: KillCreature,
            KillCreatureType, FreeCreature, ClearLocation, ExploreLocation,
            CollectItem, GiveItems, GiveItemKind, DeliverItem, or InteractWithProp. Follow their
            normal entity and required-field rules. Respond with raw JSON only.
            """;
        var userPrompt = $"""
            Premise: {input.ChainPremise}

            Blocking fact quest: {factNode.Name}
            Description: {factNode.Description}
            Primary fact: {primaryFact.Key}
            Subject: {primaryFact.Subject}
            Value: {primaryFact.Value}
            {existingReasonInstruction}

            The new support node id is {supportNodeId}. It will have the same prerequisites as the
            blocking fact quest and will add 45 willingness when complete.

            Available entities:
            {entityList}
            """;
        var repair = await client.GetValidatedJson<FactDisclosureRepairSchema>(
            logger,
            systemPrompt,
            userPrompt,
            schema =>
                ValidateRepair(
                    schema,
                    supportNodeId,
                    entityTypesById,
                    existingReasonFact,
                    existingFactKeys
                ),
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 8192 }
        );
        return new FactDisclosureRepairResult(
            new QuestChainGeneratedFact(
                repair.ReasonFact.FactKey,
                repair.ReasonFact.Subject,
                repair.ReasonFact.Value
            ),
            ToGeneratedSupportQuest(repair, supportNodeId, factNode.PrerequisiteNodeIds)
        );
    }

    private static string? ValidateRepair(
        FactDisclosureRepairSchema repair,
        string supportNodeId,
        IReadOnlyDictionary<string, string> entityTypesById,
        QuestChainGeneratedFact? existingReasonFact,
        IReadOnlySet<string> existingFactKeys
    )
    {
        if (existingReasonFact is { } reasonFact && repair.ReasonFact.FactKey != reasonFact.Key)
        {
            return $"ReasonFact must reuse the existing key \"{reasonFact.Key}\".";
        }

        if (existingReasonFact is null && existingFactKeys.Contains(repair.ReasonFact.FactKey))
        {
            return $"ReasonFact key \"{repair.ReasonFact.FactKey}\" is already in use.";
        }

        if (
            repair.SupportQuest.Objectives.Any(objective =>
                objective.ObjectiveType == nameof(GeneratedObjectiveType.LearnFactFromCreature)
            )
        )
        {
            return "The support quest cannot use LearnFactFromCreature.";
        }

        var schema = new QuestChainSchema
        {
            Facts =
            [
                new QuestChainFactSchema
                {
                    Key = repair.ReasonFact.FactKey,
                    Subject = repair.ReasonFact.Subject,
                    Value = repair.ReasonFact.Value,
                },
            ],
            Nodes =
            [
                new QuestChainNodeSchema
                {
                    NodeId = supportNodeId,
                    Name = repair.SupportQuest.Name,
                    Description = repair.SupportQuest.Description,
                    GiverEntityId = repair.SupportQuest.GiverEntityId,
                    RequiredFactKey = repair.ReasonFact.FactKey,
                    Objectives = repair
                        .SupportQuest.Objectives.Select(objective => new QuestChainObjectiveSchema
                        {
                            Name = objective.Name,
                            Description = objective.Description,
                            ObjectiveType = objective.ObjectiveType,
                            TargetEntityId = objective.TargetEntityId,
                            RecipientEntityId = objective.RecipientEntityId,
                            ItemNameForKind = objective.ItemNameForKind,
                            NewItemName = objective.NewItemName,
                            NewPropName = objective.NewPropName,
                            CreatureTypeCategory = objective.CreatureTypeCategory,
                            RequiredAmount = objective.RequiredAmount,
                        })
                        .ToList(),
                },
            ],
        };
        return QuestChainGenerator.ValidateFields(schema, entityTypesById, expectedNodeCount: 1);
    }

    private static QuestChainGeneratedNode ToGeneratedSupportQuest(
        FactDisclosureRepairSchema repair,
        string supportNodeId,
        IReadOnlyList<string> prerequisiteNodeIds
    ) =>
        new(
            supportNodeId,
            repair.SupportQuest.Name,
            repair.SupportQuest.Description,
            Guid.Parse(repair.SupportQuest.GiverEntityId!),
            repair.ReasonFact.FactKey,
            null,
            null,
            prerequisiteNodeIds,
            repair
                .SupportQuest.Objectives.Select(objective => new QuestChainGeneratedObjective(
                    objective.Name,
                    objective.Description,
                    Enum.Parse<GeneratedObjectiveType>(objective.ObjectiveType),
                    objective.TargetEntityId is { } targetId ? Guid.Parse(targetId) : null,
                    objective.RecipientEntityId is { } recipientId ? Guid.Parse(recipientId) : null,
                    objective.ItemNameForKind,
                    objective.NewItemName,
                    objective.NewPropName,
                    objective.CreatureTypeCategory is { } category
                        ? Enum.Parse<CreatureType>(category, true)
                        : null,
                    objective.RequiredAmount ?? 1,
                    null,
                    null,
                    null,
                    null,
                    null,
                    [],
                    []
                ))
                .ToArray()
        );

    private static bool BlocksContinuation(
        QuestChainGeneratedNode factNode,
        IReadOnlyList<QuestChainGeneratedNode> nodes
    ) =>
        nodes.Any(node =>
            node.PrerequisiteNodeIds.Contains(factNode.NodeId)
            && !HasAlternativeSibling(node, factNode, nodes)
        );

    private static bool HasAlternativeSibling(
        QuestChainGeneratedNode dependentNode,
        QuestChainGeneratedNode factNode,
        IReadOnlyList<QuestChainGeneratedNode> nodes
    ) =>
        factNode.GroupIndex is { } groupIndex
        && dependentNode.PrerequisiteNodeIds.Any(prerequisiteNodeId =>
            prerequisiteNodeId != factNode.NodeId
            && nodes.Single(node => node.NodeId == prerequisiteNodeId).GroupIndex == groupIndex
        );

    private static bool HasSupportingQuest(QuestChainGeneratedObjective objective) =>
        objective.WeightedSupportingQuestNodeIds.Count > 0;

    private static int GetNextNodeNumber(IReadOnlyList<QuestChainGeneratedNode> nodes) =>
        nodes.Max(node => int.Parse(node.NodeId[5..])) + 1;
}

internal record FactDisclosureRepairResult(
    QuestChainGeneratedFact ReasonFact,
    QuestChainGeneratedNode SupportQuest
);
