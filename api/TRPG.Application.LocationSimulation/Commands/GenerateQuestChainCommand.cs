using System.Text;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Books.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class GenerateQuestChainCommand
{
    public required Guid RequestId { get; init; }
    public required string ChainPremise { get; init; }
    public required int MinimumChainLength { get; init; }
    public required int MaximumChainLength { get; init; }
    public required IReadOnlyList<QuestChainCandidateEntity> AvailableEntities { get; init; }
    public IReadOnlyList<QuestChainFactionStanding> FactionStandings { get; init; } = [];
    public Guid? ForcedGiverFactionId { get; init; }
}

// TickerQ scheduling (ITimeTickerManager) is host-only — this is the same kind of boundary
// IChatClient is: an external-infrastructure dependency the host wires up concretely, so
// LocationSimulation can enqueue a background job without taking a TickerQ package reference.
public interface IQuestChainGenerationScheduler
{
    Task ScheduleAsync(
        GenerateQuestChainCommand command,
        CancellationToken cancellationToken = default
    );
}

// Runs inside the background job — the slow half of chain seeding (SeedLlmQuestChainCommand does
// the fast eligibility check and enqueues this). Always leaves the request in a terminal state
// (Completed or Failed): a request stuck in Pending/InProgress forever would mean a giver can never
// offer this chain, the same failure shape as the historical stuck CreateWorldJob bug.
internal class GenerateQuestChainCommandHandler(
    ILocationSimulationDbContext context,
    QuestChainTreatmentFirstGenerator treatmentFirstGenerator,
    QuestChainFactDisclosureRepairer factDisclosureRepairer,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
    ICommandHandler<AddFactsCommand> addFacts,
    ICommandHandler<AddQuestCommand> addQuest,
    ICommandHandler<AddItemsCommand> addItems,
    ILogger<GenerateQuestChainCommandHandler> logger
) : ICommandHandler<GenerateQuestChainCommand, bool>
{
    private const int GoldRewardPerNode = 50;
    private const int GiverReputationRewardPerNode = 15;

    public async Task<bool> Handle(
        GenerateQuestChainCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var request = await context.QuestChainGenerationRequests.FindAsync(
            [command.RequestId],
            cancellationToken
        );
        if (request == null)
        {
            return false;
        }

        if (request.Status != QuestChainGenerationStatus.Pending)
        {
            return false;
        }

        request.Status = QuestChainGenerationStatus.InProgress;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var generatorInput = new QuestChainGeneratorInput
            {
                ChainPremise = command.ChainPremise,
                MinimumChainLength = command.MinimumChainLength,
                MaximumChainLength = command.MaximumChainLength,
                AvailableEntities = command.AvailableEntities,
                FactionStandings = command.FactionStandings,
                ForcedGiverFactionId = command.ForcedGiverFactionId,
            };
            var generatedChain = await treatmentFirstGenerator.Generate(
                generatorInput,
                cancellationToken
            );
            generatedChain = await factDisclosureRepairer.Repair(
                generatorInput,
                generatedChain,
                cancellationToken
            );

            LogGeneratedChain(command, generatedChain);

            await Persist(
                request.WorldId,
                command.AvailableEntities,
                generatedChain,
                cancellationToken
            );

            request.Status = QuestChainGenerationStatus.Completed;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Quest chain generation failed for request {RequestId}",
                command.RequestId
            );
            request.Status = QuestChainGenerationStatus.Failed;
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return request.Status == QuestChainGenerationStatus.Completed;
    }

    // Logged before persistence so the generated shape is visible even if mapping/persistence
    // later throws — useful for inspecting what the LLM actually produced, not just whether it
    // ultimately succeeded.
    private void LogGeneratedChain(
        GenerateQuestChainCommand command,
        QuestChainGeneratedResult generatedChain
    )
    {
        var entityNamesById = command.AvailableEntities.ToDictionary(
            entity => entity.Id,
            entity => entity.Name
        );
        string DescribeEntity(Guid id) => entityNamesById.GetValueOrDefault(id, id.ToString());

        var builder = new StringBuilder();
        builder.AppendLine(
            $"Generated quest chain for request {command.RequestId} ({generatedChain.Nodes.Count} nodes):"
        );
        foreach (var fact in generatedChain.Facts)
        {
            builder.AppendLine($"Fact [{fact.Key}] {fact.Subject}: {fact.Value}");
        }
        foreach (var node in generatedChain.Nodes)
        {
            var prerequisites =
                node.PrerequisiteNodeIds.Count == 0
                    ? "none"
                    : string.Join(", ", node.PrerequisiteNodeIds);
            builder.AppendLine(
                $"[{node.NodeId}] \"{node.Name}\" — giver: {DescribeEntity(node.GiverEntityId)}, prerequisites: {prerequisites}, exclusive group: {node.GroupIndex?.ToString() ?? "none"}, prerequisite alternative group: {node.PrerequisiteAlternativeGroupIndex?.ToString() ?? "none"}"
            );
            builder.AppendLine($"    {node.Description}");
            foreach (var objective in node.Objectives)
            {
                var detail = objective.ObjectiveType switch
                {
                    GeneratedObjectiveType.KillCreatureType =>
                        $"category={objective.CreatureTypeCategory}, amount={objective.RequiredAmount}",
                    GeneratedObjectiveType.GiveItemKind =>
                        $"item=\"{objective.ItemNameForKind}\", amount={objective.RequiredAmount}, recipient={DescribeEntity(objective.RecipientEntityId!.Value)}",
                    GeneratedObjectiveType.CollectItem
                    or GeneratedObjectiveType.GiveItems
                    or GeneratedObjectiveType.DeliverItem =>
                        $"newItem=\"{objective.NewItemName}\", heldBy={DescribeEntity(objective.TargetEntityId!.Value)}"
                            + (
                                objective.RecipientEntityId is { } itemRecipientId
                                    ? $", recipient={DescribeEntity(itemRecipientId)}"
                                    : ""
                            ),
                    _ => $"target={DescribeEntity(objective.TargetEntityId!.Value)}"
                        + (
                            objective.RecipientEntityId is { } recipientId
                                ? $", recipient={DescribeEntity(recipientId)}"
                                : ""
                        )
                        + (
                            objective.RequiredAmount > 1
                                ? $", amount={objective.RequiredAmount}"
                                : ""
                        ),
                };
                builder.AppendLine(
                    $"  - [{objective.ObjectiveType}] \"{objective.Name}\": {detail}"
                );
            }
        }

        logger.LogInformation("{QuestChain}", builder.ToString());
    }

    private async Task Persist(
        Guid worldId,
        IReadOnlyList<QuestChainCandidateEntity> availableEntities,
        QuestChainGeneratedResult generatedChain,
        CancellationToken cancellationToken
    )
    {
        var nodes = generatedChain.Nodes;
        await ValidateCurrentEntities(
            worldId,
            availableEntities,
            generatedChain,
            cancellationToken
        );
        var buildings = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var buildingsById = buildings.ToDictionary(building => building.Id);

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var factIdByKey = generatedChain.Facts.ToDictionary(fact => fact.Key, _ => Guid.NewGuid());
        var facts = generatedChain
            .Facts.Select(fact => new Fact
            {
                Id = factIdByKey[fact.Key],
                WorldId = worldId,
                Subject = fact.Subject,
                Value = fact.Value,
            })
            .ToArray();
        var questIdByNodeId = nodes.ToDictionary(node => node.NodeId, _ => Guid.NewGuid());
        var groupIdByIndex = nodes
            .SelectMany(node => new[] { node.GroupIndex, node.PrerequisiteAlternativeGroupIndex })
            .Where(groupIndex => groupIndex != null)
            .Select(groupIndex => groupIndex!.Value)
            .Distinct()
            .ToDictionary(groupIndex => groupIndex, _ => Guid.NewGuid());
        var newItems = new List<Item>();
        var terminalNodeId = nodes[^1].NodeId;

        // Build every quest and its objectives in memory first (minting new Item instances for
        // CollectItem/GiveItems/DeliverItem into newItems along the way), so all newly-minted items
        // can be persisted in one batch before any quest that references them is saved.
        var questBuilds = nodes
            .Select(node =>
            {
                var quest = new Quest
                {
                    Id = questIdByNodeId[node.NodeId],
                    WorldId = worldId,
                    GiverId = node.GiverEntityId,
                    Name = node.Name,
                    Description = node.Description,
                    GoldReward = GoldRewardPerNode,
                    ExclusiveGroupId = node.GroupIndex is { } groupIndex
                        ? groupIdByIndex[groupIndex]
                        : null,
                    PrerequisiteAlternativeGroupId = node.PrerequisiteAlternativeGroupIndex
                        is { } prerequisiteAlternativeGroupIndex
                        ? groupIdByIndex[prerequisiteAlternativeGroupIndex]
                        : null,
                    RequiredFactId = node.RequiredFactKey is { } factKey
                        ? factIdByKey[factKey]
                        : null,
                    RequiredFactionId =
                        node.PrerequisiteNodeIds.Count == 0 ? generatedChain.GiverFactionId : null,
                    ChainGiverFactionId = generatedChain.GiverFactionId,
                    ChainAntagonistFactionId = generatedChain.AntagonistFactionId,
                    IsChainTerminal = node.NodeId == terminalNodeId,
                    PrerequisiteQuestIds = node
                        .PrerequisiteNodeIds.Select(nodeId => questIdByNodeId[nodeId])
                        .ToList(),
                };
                quest.ReputationRewards.Add(
                    new QuestReputationReward
                    {
                        WorldId = worldId,
                        QuestId = quest.Id,
                        TargetId = node.GiverEntityId,
                        TargetType = ReputationTargetType.Creature,
                        Score = GiverReputationRewardPerNode,
                    }
                );

                var objectives = node
                    .Objectives.Select(objective =>
                        MapObjective(
                            worldId,
                            quest.Id,
                            objective,
                            questIdByNodeId,
                            factIdByKey,
                            buildingsById,
                            newItems
                        )
                    )
                    .ToArray();

                return (Quest: quest, Objectives: objectives);
            })
            .ToArray();

        if (facts.Length > 0)
        {
            await addFacts.Handle(new AddFactsCommand { Facts = facts }, cancellationToken);
        }

        if (newItems.Count > 0)
        {
            await addItems.Handle(new AddItemsCommand { Items = newItems }, cancellationToken);
        }

        foreach (var (quest, objectives) in questBuilds)
        {
            await addQuest.Handle(
                new AddQuestCommand { Quest = quest, Objectives = objectives },
                cancellationToken
            );
        }

        transaction.Complete();
    }

    private async Task ValidateCurrentEntities(
        Guid worldId,
        IReadOnlyList<QuestChainCandidateEntity> availableEntities,
        QuestChainGeneratedResult generatedChain,
        CancellationToken cancellationToken
    )
    {
        var entityTypeById = availableEntities.ToDictionary(
            entity => entity.Id,
            entity => entity.Type
        );
        var referencedCreatureIds = generatedChain
            .Nodes.SelectMany(node =>
                node.Objectives.SelectMany(objective =>
                    new[]
                    {
                        node.GiverEntityId,
                        objective.TargetEntityId,
                        objective.RecipientEntityId,
                    }
                )
            )
            .Where(id => id is { } value && entityTypeById[value] == QuestChainEntityTypes.Creature)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = referencedCreatureIds },
            cancellationToken
        );
        if (
            creatures.Count != referencedCreatureIds.Length
            || creatures.Values.Any(creature =>
                creature.WorldId != worldId || creature.State == CreatureState.Dead
            )
        )
        {
            throw new InvalidOperationException(
                "A generated quest-chain creature is no longer available."
            );
        }
    }

    // A polymorphic dispatcher over the 10 real QuestObjective subtypes — Validate() in
    // QuestChainGenerator already guarantees every field this switch reads is present and of the
    // right entity type, so the ! null-forgiving operators here are asserting an invariant already
    // enforced upstream, not skipping a check. CollectItem/GiveItems/DeliverItem don't reference an
    // existing item — none exists yet — so their branches mint a brand-new Item into newItems,
    // owned by whichever existing creature TargetEntityId points at, and reference its new id.
    private static QuestObjective MapObjective(
        Guid worldId,
        Guid questId,
        QuestChainGeneratedObjective objective,
        IReadOnlyDictionary<string, Guid> questIdByNodeId,
        IReadOnlyDictionary<string, Guid> factIdByKey,
        IReadOnlyDictionary<Guid, Building> buildingsById,
        List<Item> newItems
    ) =>
        objective.ObjectiveType switch
        {
            GeneratedObjectiveType.KillCreature => new KillCreatureObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                CreatureId = objective.TargetEntityId!.Value,
            },
            GeneratedObjectiveType.KillCreatureType => new KillCreatureTypeObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                CreatureType = objective.CreatureTypeCategory!.Value,
                RequiredAmount = objective.RequiredAmount,
            },
            GeneratedObjectiveType.FreeCreature => new FreeCreatureObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                CreatureId = objective.TargetEntityId!.Value,
            },
            GeneratedObjectiveType.ClearLocation => new ClearLocationObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                BuildingId = objective.TargetEntityId!.Value,
                LocationId = buildingsById[objective.TargetEntityId!.Value].ExteriorLocationId,
                RequiredAmount = objective.RequiredAmount,
            },
            GeneratedObjectiveType.ExploreLocation => new ExploreLocationObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                LocationId = buildingsById[objective.TargetEntityId!.Value].ExteriorLocationId,
            },
            GeneratedObjectiveType.LearnFactFromCreature => new LearnFactFromCreatureObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                CreatureId = objective.TargetEntityId!.Value,
                FactId = factIdByKey[objective.FactKey!],
                ReasonFactId = objective.ReasonFactKey is { } reasonFactKey
                    ? factIdByKey[reasonFactKey]
                    : null,
                BaseWillingness = objective.BaseWillingness!.Value,
                BribeWillingness = objective.BribeWillingness!.Value,
                IntimidationWillingness = objective.IntimidationWillingness!.Value,
                RequiredSupportingQuestIds = objective
                    .RequiredSupportingQuestNodeIds.Select(nodeId => questIdByNodeId[nodeId])
                    .ToList(),
                WeightedSupportingQuestIds = objective
                    .WeightedSupportingQuestNodeIds.Select(support => new SupportingFactQuestWeight
                    {
                        QuestId = questIdByNodeId[support.NodeId],
                        Weight = support.Weight,
                    })
                    .ToList(),
            },
            GeneratedObjectiveType.ReportFactToCreature => new ReportFactToCreatureObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                CreatureId = objective.TargetEntityId!.Value,
                FactId = factIdByKey[objective.FactKey!],
            },
            GeneratedObjectiveType.CollectItem => new CollectItemObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                ItemId = MintItem(worldId, objective, newItems).Id,
            },
            GeneratedObjectiveType.GiveItems => new GiveItemsObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                ItemIds = [MintItem(worldId, objective, newItems).Id],
                RecipientId = objective.RecipientEntityId!.Value,
                RequiredAmount = objective.RequiredAmount,
            },
            GeneratedObjectiveType.GiveItemKind => new GiveItemKindObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                ItemName = objective.ItemNameForKind!,
                RecipientId = objective.RecipientEntityId!.Value,
                RequiredAmount = objective.RequiredAmount,
            },
            GeneratedObjectiveType.DeliverItem => new DeliverItemObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                ItemId = MintItem(worldId, objective, newItems).Id,
                RecipientId = objective.RecipientEntityId!.Value,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(objective)),
        };

    private static Item MintItem(
        Guid worldId,
        QuestChainGeneratedObjective objective,
        List<Item> newItems
    )
    {
        var item = new Item
        {
            WorldId = worldId,
            Name = objective.NewItemName!,
            Description = objective.Description,
            Quantity = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = objective.TargetEntityId!.Value,
                OwnerType = OwnerType.Creature,
            },
        };
        newItems.Add(item);
        return item;
    }
}
