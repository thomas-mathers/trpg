using System.Transactions;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class MovePlayerCommand
{
    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid SessionId { get; init; }

    [NotBlank]
    public required string DestinationName { get; init; }
}

public record MovePlayerResult(
    EntryOutcome Outcome,
    Creature Player,
    HostileEncounterState? Encounter = null,
    SceneResult? Scene = null
);

internal class MovePlayerCommandHandler(
    IDomainEventPublisher<PlayerMovedEvent> domainEvents,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureSummary>
    > getCreaturesAtLocation,
    IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>> getActiveQuestItemIds,
    IQueryHandler<
        GetCreatureIdsHoldingItemsQuery,
        IReadOnlyCollection<Guid>
    > getCreatureIdsHoldingItems,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<DeleteCreaturesCommand> deleteCreatures,
    IQueryHandler<GetBuildingByNameAtLocationQuery, Building?> getBuildingByNameAtLocation,
    IQueryHandler<GetExitByDestinationNameQuery, ExitMatch> getExitByDestinationName,
    IQueryHandler<
        GetBuildingEntryRequirementsQuery,
        BuildingEntryRequirements
    > getBuildingEntryRequirements,
    IQueryHandler<GetInventoryByOwnerQuery, IReadOnlyList<Item>> getInventoryByOwner,
    ICommandHandler<SyncScheduleLockCommand, bool?> syncScheduleLock,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetActiveEncounterQuery, HostileEncounter?> getActiveEncounter,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<
        EvaluateLocationEncountersCommand,
        HostileEncounterState?
    > evaluateLocationEncounters,
    SceneCatchUpCache catchUpCache,
    ILogger<MovePlayerCommandHandler> logger
) : ICommandHandler<MovePlayerCommand, MovePlayerResult>
{
    public async Task<MovePlayerResult> Handle(
        MovePlayerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );

        if (activeEncounter != null)
        {
            transaction.Complete();
            return new MovePlayerResult(EntryOutcome.EncounterActive, player!);
        }

        var oldLocationId = player!.LocationId;

        var currentLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );

        var outcome = await MoveFromLocation(player, currentLocation!, command, cancellationToken);

        if (outcome != EntryOutcome.Entered)
        {
            transaction.Complete();
            return new MovePlayerResult(outcome, player);
        }

        await CleanUpDeadCreatures(player, oldLocationId, cancellationToken);
        await BustAlertedCreaturesCache(
            player,
            oldLocationId,
            command.SessionId,
            cancellationToken
        );

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [player.Id],
                LocationId = player.LocationId,
            },
            cancellationToken
        );

        await domainEvents.Publish(
            new PlayerMovedEvent(
                PlayerId: player.Id,
                WorldId: player.WorldId,
                LocationId: player.LocationId
            ),
            cancellationToken
        );

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = player.WorldId,
                PlayerId = player.Id,
                SessionId = command.SessionId,
            },
            cancellationToken
        );

        var encounter = await evaluateLocationEncounters.Handle(
            new EvaluateLocationEncountersCommand
            {
                WorldId = player.WorldId,
                PlayerId = player.Id,
                OriginLocationId = oldLocationId,
            },
            cancellationToken
        );

        transaction.Complete();
        return new MovePlayerResult(EntryOutcome.Entered, player, encounter, refreshed.Scene);
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
        var removableCreatureIds = deadCreatureIds.Except(questItemOwnerIds).ToArray();

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

    private async Task BustAlertedCreaturesCache(
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

        var hasAlertedCreature = nearby.Any(creature => creature.State == CreatureState.Alerted);
        if (!hasAlertedCreature)
        {
            return;
        }

        var schedulePlaytime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = sessionId },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(schedulePlaytime);

        catchUpCache.Evict(player.WorldId, oldLocationId, currentDate.Hour);
    }

    private async Task<EntryOutcome> MoveFromLocation(
        Creature player,
        Location currentLocation,
        MovePlayerCommand command,
        CancellationToken cancellationToken
    )
    {
        if (currentLocation.RoomId == null)
        {
            var buildingOutcome = await TryEnterBuildingByName(
                player,
                currentLocation,
                command,
                cancellationToken
            );
            if (buildingOutcome != null)
            {
                return buildingOutcome.Value;
            }
        }

        var exitMatch = await getExitByDestinationName.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = currentLocation.Id,
                DestinationName = command.DestinationName,
            },
            cancellationToken
        );

        if (!exitMatch.Matched)
        {
            return currentLocation.RoomId == null
                ? EntryOutcome.DestinationNotFound
                : EntryOutcome.ExitNotFound;
        }

        player.LocationId = exitMatch.DestinationLocationId!.Value;

        return EntryOutcome.Entered;
    }

    private async Task<EntryOutcome?> TryEnterBuildingByName(
        Creature player,
        Location currentLocation,
        MovePlayerCommand command,
        CancellationToken cancellationToken
    )
    {
        var building = await getBuildingByNameAtLocation.Handle(
            new GetBuildingByNameAtLocationQuery
            {
                LocationId = currentLocation.Id,
                Name = command.DestinationName,
            },
            cancellationToken
        );

        if (building == null)
        {
            return null;
        }

        var schedulePlaytime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(schedulePlaytime);

        await syncScheduleLock.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        var requirements = await getBuildingEntryRequirements.Handle(
            new GetBuildingEntryRequirementsQuery { BuildingId = building.Id },
            cancellationToken
        );

        if (requirements.Outcome == BuildingEntryOutcome.NoEntrance)
        {
            return EntryOutcome.NoEntrance;
        }

        if (
            requirements.Outcome == BuildingEntryOutcome.Locked
            && !await HasAnyKey(player, requirements, cancellationToken)
        )
        {
            return EntryOutcome.Locked;
        }

        player.LocationId = requirements.EntranceLocationId!.Value;

        return EntryOutcome.Entered;
    }

    private async Task<bool> HasAnyKey(
        Creature player,
        BuildingEntryRequirements requirements,
        CancellationToken cancellationToken
    )
    {
        var inventory = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(player.Id, OwnerType.Creature),
            },
            cancellationToken
        );
        return inventory.Any(item => requirements.ValidKeyItemIds!.Contains(item.Id));
    }
}
