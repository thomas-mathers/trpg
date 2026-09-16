using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
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

        foreach (var node in nodes)
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
                    MapObjective(worldId, quest.Id, objective, buildingsById)
                )
                .ToArray();

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
    // enforced upstream, not skipping a check.
    private static QuestObjective MapObjective(
        Guid worldId,
        Guid questId,
        QuestChainGeneratedObjective objective,
        IReadOnlyDictionary<Guid, Building> buildingsById
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
                ItemId = objective.TargetEntityId!.Value,
            },
            GeneratedObjectiveType.GiveItems => new GiveItemsObjective
            {
                WorldId = worldId,
                QuestId = questId,
                Name = objective.Name,
                Description = objective.Description,
                ItemIds = [objective.TargetEntityId!.Value],
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
                ItemId = objective.TargetEntityId!.Value,
                RecipientId = objective.RecipientEntityId!.Value,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(objective)),
        };
}
