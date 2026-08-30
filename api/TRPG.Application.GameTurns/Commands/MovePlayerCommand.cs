using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
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
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<ResolveKillCrimesCommand> resolveKillCrimes,
    ICommandHandler<ResolveTheftCrimesCommand> resolveTheftCrimes,
    ICommandHandler<CleanUpAbandonedCorpsesCommand> cleanUpAbandonedCorpses,
    ICommandHandler<ResetAlertedCreaturesCommand> resetAlertedCreatures,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult> evaluateEncounters,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted
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
                    SessionId = command.SessionId,
                },
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

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = player.Id,
                Encounter = evaluation.HostileEncounter ?? (Encounter?)evaluation.GuardEncounter,
            },
            cancellationToken
        );

        return new MovePlayerResult(
            player,
            evaluation.HostileEncounter,
            evaluation.GuardEncounter,
            refreshed.Scene
        );
    }
}
