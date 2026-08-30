using System.Transactions;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class MovePlayerCommand
{
    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid SessionId { get; init; }

    [NotEmptyGuid]
    public required Guid DestinationLocationId { get; init; }
}

public record MovePlayerResult(
    Creature Player,
    HostileEncounter? Encounter,
    GuardEncounter? GuardEncounter,
    SceneResult Scene
);

internal class MovePlayerCommandHandler(
    IDomainEventPublisher<PlayerMovedEvent> domainEvents,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation,
    IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>> getActiveQuestItemIds,
    IQueryHandler<
        GetCreatureIdsHoldingItemsQuery,
        IReadOnlyCollection<Guid>
    > getCreatureIdsHoldingItems,
    IQueryHandler<
        GetInventoryItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getInventoryItemsByOwners,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<DeleteCreaturesCommand> deleteCreatures,
    ICommandHandler<ResolveKillCrimesCommand> resolveKillCrimes,
    ICommandHandler<ResolveTheftCrimesCommand> resolveTheftCrimes,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult> evaluateEncounters,
    SceneCatchUpCache catchUpCache,
    IGameClientEventSink gameEvents,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    ILogger<MovePlayerCommandHandler> logger
) : ICommandHandler<MovePlayerCommand, MovePlayerResult>
{
    public async Task<MovePlayerResult> Handle(
        MovePlayerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        Creature player;
        RefreshSceneResult refreshed;
        EncounterEvaluationResult evaluation;

        using (
            var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            )
        )
        {
            player = (
                await getCreatureById.Handle(
                    new GetCreatureByIdQuery { Id = command.PlayerId },
                    cancellationToken
                )
            )!;

            var oldLocationId = player.LocationId;

            await resolveKillCrimes.Handle(
                new ResolveKillCrimesCommand
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                },
                cancellationToken
            );

            await resolveTheftCrimes.Handle(
                new ResolveTheftCrimesCommand
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                },
                cancellationToken
            );

            await CleanUpDeadCreatures(player, oldLocationId, cancellationToken);

            await ResetAlertedCreatures(
                player,
                oldLocationId,
                command.SessionId,
                cancellationToken
            );

            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = [player.Id],
                    LocationId = command.DestinationLocationId,
                },
                cancellationToken
            );

            await domainEvents.Publish(
                new PlayerMovedEvent(
                    PlayerId: player.Id,
                    WorldId: player.WorldId,
                    LocationId: command.DestinationLocationId
                ),
                cancellationToken
            );

            refreshed = await refreshScene.Handle(
                new RefreshSceneCommand
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    SessionId = command.SessionId,
                },
                cancellationToken
            );

            evaluation = await evaluateEncounters.Handle(
                new EvaluateEncountersCommand { WorldId = player.WorldId, PlayerId = player.Id },
                cancellationToken
            );

            transaction.Complete();
        }

        await PublishEncounterStarted(player.Id, evaluation, cancellationToken);

        return new MovePlayerResult(
            player,
            evaluation.HostileEncounter,
            evaluation.GuardEncounter,
            refreshed.Scene
        );
    }

    private async Task PublishEncounterStarted(
        Guid playerId,
        EncounterEvaluationResult evaluation,
        CancellationToken cancellationToken
    )
    {
        if (evaluation.HostileEncounter is { } hostileEncounter)
        {
            gameEvents.Enqueue(new HostileEncounterStartedEvent(hostileEncounter));
            return;
        }

        if (evaluation.GuardEncounter is { } guardEncounter)
        {
            var playerGold = await getGoldQuantity.Handle(
                new GetGoldQuantityQuery
                {
                    Owner = new ItemOwnerReference(playerId, OwnerType.Creature),
                },
                cancellationToken
            );
            gameEvents.Enqueue(
                new GuardEncounterStartedEvent(
                    guardEncounter,
                    playerGold >= guardEncounter.FineAmount
                )
            );
        }
    }

    private async Task CleanUpDeadCreatures(
        Creature player,
        Guid oldLocationId,
        CancellationToken cancellationToken
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = player.WorldId,
                LocationId = oldLocationId,
            },
            cancellationToken
        );

        var deadCreatureIds = nearby
            .Where(creature => creature.State == CreatureState.Dead)
            .Select(creature => creature.Id)
            .ToArray();

        if (deadCreatureIds.Length == 0)
        {
            return;
        }

        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = player.Id },
            cancellationToken
        );
        var questItemOwnerIds = await getCreatureIdsHoldingItems.Handle(
            new GetCreatureIdsHoldingItemsQuery { ItemIds = questItemIds },
            cancellationToken
        );
        var playerCorpseIds = nearby
            .Where(creature => creature.PlayerCorpseOwnerId != null)
            .Select(creature => creature.Id)
            .ToArray();
        var itemsByPlayerCorpse = await getInventoryItemsByOwners.Handle(
            new GetInventoryItemsByOwnersQuery { CreatureIds = playerCorpseIds },
            cancellationToken
        );
        var unlootedPlayerCorpseIds = playerCorpseIds.Where(itemsByPlayerCorpse.ContainsKey);
        var removableCreatureIds = deadCreatureIds
            .Except(questItemOwnerIds)
            .Except(unlootedPlayerCorpseIds)
            .ToArray();

        if (removableCreatureIds.Length == 0)
        {
            return;
        }

        logger.LogInformation(
            "[move] deleting {Count} dead creature(s) left behind: {CreatureIds}",
            removableCreatureIds.Length,
            string.Join(", ", removableCreatureIds)
        );

        await deleteCreatures.Handle(
            new DeleteCreaturesCommand { CreatureIds = removableCreatureIds },
            cancellationToken
        );
    }

    private async Task ResetAlertedCreatures(
        Creature player,
        Guid oldLocationId,
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = player.WorldId,
                LocationId = oldLocationId,
            },
            cancellationToken
        );

        var alertedCreatureIds = nearby
            .Where(creature => creature.State == CreatureState.Alerted)
            .Select(creature => creature.Id)
            .ToArray();

        if (alertedCreatureIds.Length == 0)
        {
            return;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = alertedCreatureIds,
                State = CreatureState.Idle,
            },
            cancellationToken
        );

        var schedulePlaytime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = sessionId },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(schedulePlaytime);

        catchUpCache.Evict(player.WorldId, oldLocationId, currentDate.Hour);
    }
}
