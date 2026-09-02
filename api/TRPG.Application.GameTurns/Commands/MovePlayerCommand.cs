using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Worlds.Queries;
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
    TheftEncounter? OverdueRoomKeyEncounter,
    SceneResult Scene
);

internal class MovePlayerCommandHandler(
    IDomainEventPublisher<PlayerMovedEvent> domainEvents,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IQueryHandler<
        GetKillWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getKillWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveKillCrimeWitnessesCommand,
        ResolveKillCrimeWitnessesResult
    > resolveKillCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForKillsCommand> applyReputationPenaltyForKills,
    IQueryHandler<
        GetTheftWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getTheftWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveTheftCrimeWitnessesCommand,
        ResolveTheftCrimeWitnessesResult
    > resolveTheftCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForTheftsCommand> applyReputationPenaltyForThefts,
    ICommandHandler<CleanUpAbandonedCorpsesCommand> cleanUpAbandonedCorpses,
    ICommandHandler<ResetAlertedCreaturesCommand> resetAlertedCreatures,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult> evaluateEncounters,
    ICommandHandler<
        ConfrontOverdueRoomKeyCommand,
        ConfrontOverdueRoomKeyResult
    > confrontOverdueRoomKey,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime
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
        TheftEncounter? overdueRoomKeyEncounter;

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

            var playtime = await getPlaytime.Handle(
                new GetPlaytimeQuery { SessionId = command.SessionId },
                cancellationToken
            );

            var killWitnessCandidateIds = await getKillWitnessCandidateCreatureIds.Handle(
                new GetKillWitnessCandidateCreatureIdsQuery
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                },
                cancellationToken
            );
            var liveKillWitnessIds = await ResolveLiveCreatureIds(
                killWitnessCandidateIds,
                cancellationToken
            );

            var killResolution = await resolveKillCrimeWitnesses.Handle(
                new ResolveKillCrimeWitnessesCommand
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                    LiveWitnessCreatureIds = liveKillWitnessIds,
                },
                cancellationToken
            );
            if (killResolution.ReportedCrimes.Count > 0)
            {
                await applyReputationPenaltyForKills.Handle(
                    new ApplyReputationPenaltyForKillsCommand
                    {
                        KillerId = player.Id,
                        WorldId = player.WorldId,
                        Kills = killResolution.ReportedCrimes,
                    },
                    cancellationToken
                );
            }

            var theftWitnessCandidateIds = await getTheftWitnessCandidateCreatureIds.Handle(
                new GetTheftWitnessCandidateCreatureIdsQuery
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                },
                cancellationToken
            );
            var liveTheftWitnessIds = await ResolveLiveCreatureIds(
                theftWitnessCandidateIds,
                cancellationToken
            );

            var theftResolution = await resolveTheftCrimeWitnesses.Handle(
                new ResolveTheftCrimeWitnessesCommand
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                    LiveWitnessCreatureIds = liveTheftWitnessIds,
                },
                cancellationToken
            );
            if (theftResolution.ReportedCrimes.Count > 0)
            {
                await applyReputationPenaltyForThefts.Handle(
                    new ApplyReputationPenaltyForTheftsCommand
                    {
                        PlayerId = player.Id,
                        WorldId = player.WorldId,
                        Thefts = theftResolution.ReportedCrimes,
                    },
                    cancellationToken
                );
            }

            await cleanUpAbandonedCorpses.Handle(
                new CleanUpAbandonedCorpsesCommand
                {
                    WorldId = player.WorldId,
                    PlayerId = player.Id,
                    LocationId = oldLocationId,
                },
                cancellationToken
            );

            await resetAlertedCreatures.Handle(
                new ResetAlertedCreaturesCommand
                {
                    WorldId = player.WorldId,
                    LocationId = oldLocationId,
                    Playtime = playtime,
                },
                cancellationToken
            );

            overdueRoomKeyEncounter = await ResolveOverdueRoomKeyConfrontation(
                player,
                oldLocationId,
                command,
                playtime,
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
                    Playtime = playtime,
                },
                cancellationToken
            );

            evaluation = await evaluateEncounters.Handle(
                new EvaluateEncountersCommand { WorldId = player.WorldId, PlayerId = player.Id },
                cancellationToken
            );

            transaction.Complete();
        }

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = player.Id,
                Encounter =
                    evaluation.HostileEncounter
                    ?? (Encounter?)evaluation.GuardEncounter
                    ?? overdueRoomKeyEncounter,
            },
            cancellationToken
        );

        return new MovePlayerResult(
            player,
            evaluation.HostileEncounter,
            evaluation.GuardEncounter,
            overdueRoomKeyEncounter,
            refreshed.Scene
        );
    }

    private async Task<TheftEncounter?> ResolveOverdueRoomKeyConfrontation(
        Creature player,
        Guid oldLocationId,
        MovePlayerCommand command,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        var oldBuilding = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = oldLocationId },
            cancellationToken
        );
        var newBuilding = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.DestinationLocationId },
            cancellationToken
        );

        if (oldBuilding?.Id == newBuilding?.Id)
        {
            return null;
        }

        // Either direction counts — a player who left before it was due must still get caught coming back in.
        var innBuilding =
            oldBuilding is { BuildingType: BuildingType.Inn } ? oldBuilding
            : newBuilding is { BuildingType: BuildingType.Inn } ? newBuilding
            : null;
        if (innBuilding == null)
        {
            return null;
        }

        var confrontation = await confrontOverdueRoomKey.Handle(
            new ConfrontOverdueRoomKeyCommand
            {
                WorldId = player.WorldId,
                Playtime = playtime,
                PlayerId = player.Id,
                LocationId = command.DestinationLocationId,
                BuildingId = innBuilding.Id,
            },
            cancellationToken
        );

        return confrontation.Encounter;
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveLiveCreatureIds(
        IReadOnlyCollection<Guid> creatureIds,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0)
        {
            return [];
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        return creaturesById
            .Where(creature => creature.Value.State != CreatureState.Dead)
            .Select(creature => creature.Key)
            .ToArray();
    }
}
