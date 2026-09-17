using System.Text;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
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
    public required int ChainLength { get; init; }
    public required IReadOnlyList<QuestChainCandidateEntity> AvailableEntities { get; init; }
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
    QuestChainGenerator generator,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
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

        request.Status = QuestChainGenerationStatus.InProgress;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var nodes = await generator.Generate(
                new QuestChainGeneratorInput
                {
                    ChainPremise = command.ChainPremise,
                    ChainLength = command.ChainLength,
                    AvailableEntities = command.AvailableEntities,
                },
                cancellationToken
            );

            LogGeneratedChain(command, nodes);

            await Persist(request.WorldId, nodes, cancellationToken);

            request.Status = QuestChainGenerationStatus.Completed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Quest chain generation failed for request {RequestId}",
                command.RequestId
            );
            request.Status = QuestChainGenerationStatus.Failed;
        }

        await context.SaveChangesAsync(cancellationToken);
        return request.Status == QuestChainGenerationStatus.Completed;
    }

    // Logged before persistence so the generated shape is visible even if mapping/persistence
    // later throws — useful for inspecting what the LLM actually produced, not just whether it
    // ultimately succeeded.
    private void LogGeneratedChain(
        GenerateQuestChainCommand command,
        IReadOnlyList<QuestChainGeneratedNode> nodes
    )
    {
        var entityNamesById = command.AvailableEntities.ToDictionary(
            entity => entity.Id,
            entity => entity.Name
        );
        string DescribeEntity(Guid id) => entityNamesById.GetValueOrDefault(id, id.ToString());

        var builder = new StringBuilder();
        builder.AppendLine(
            $"Generated quest chain for request {command.RequestId} ({nodes.Count} nodes):"
        );
        foreach (var node in nodes)
        {
            var prerequisites =
                node.PrerequisiteNodeIds.Count == 0
                    ? "none"
                    : string.Join(", ", node.PrerequisiteNodeIds);
            builder.AppendLine(
                $"[{node.NodeId}] \"{node.Name}\" — giver: {DescribeEntity(node.GiverEntityId)}, prerequisites: {prerequisites}"
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
        IReadOnlyList<QuestChainGeneratedNode> nodes,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var buildingsById = buildings.ToDictionary(building => building.Id);

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var questIdByNodeId = nodes.ToDictionary(node => node.NodeId, _ => Guid.NewGuid());
        var newItems = new List<Item>();

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
                        MapObjective(worldId, quest.Id, objective, buildingsById, newItems)
                    )
                    .ToArray();

                return (Quest: quest, Objectives: objectives);
            })
            .ToArray();

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
            GeneratedObjectiveType.SpeakToCreature => new SpeakToCreatureObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                CreatureId = objective.TargetEntityId!.Value,
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
