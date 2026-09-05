using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Results;
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

public record MovePlayerResult(Guid PlayerLocationId, Encounter? Encounter, SceneResult Scene);

internal class MovePlayerCommandHandler(
    IDomainEventPublisher<PlayerMovedEvent> domainEvents,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult> evaluateEncounters,
    ICommandHandler<
        ConfrontOverdueRoomKeyOnMoveCommand,
        ConfrontOverdueRoomKeyResult
    > confrontOverdueRoomKeyOnMove,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime
) : ICommandHandler<MovePlayerCommand, MovePlayerResult>
{
    public async Task<MovePlayerResult> Handle(
        MovePlayerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        Guid playerLocationId;
        Encounter? startedEncounter;
        RefreshSceneResult refreshed;

        using (
            var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            )
        )
        {
            var player =
                await getCreatureById.Handle(
                    new GetCreatureByIdQuery { Id = command.PlayerId },
                    cancellationToken
                ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

            var oldLocationId = player.LocationId;

            var playtime = await getPlaytime.Handle(
                new GetPlaytimeQuery { SessionId = command.SessionId },
                cancellationToken
            );

            var confrontation = await confrontOverdueRoomKeyOnMove.Handle(
                new ConfrontOverdueRoomKeyOnMoveCommand
                {
                    WorldId = player.WorldId,
                    Playtime = playtime,
                    PlayerId = player.Id,
                    FromLocationId = oldLocationId,
                    ToLocationId = command.DestinationLocationId,
                },
                cancellationToken
            );

            var refreshSceneCommand = new RefreshSceneCommand
            {
                WorldId = player.WorldId,
                PlayerId = player.Id,
                Playtime = playtime,
            };

            if (confrontation.Encounter != null)
            {
                playerLocationId = oldLocationId;
                startedEncounter = confrontation.Encounter;
                refreshed = await refreshScene.Handle(refreshSceneCommand, cancellationToken);
            }
            else
            {
                await MoveTo(
                    player,
                    oldLocationId,
                    command.DestinationLocationId,
                    playtime,
                    cancellationToken
                );

                // The scene refresh catches the destination up before encounters are evaluated against it.
                refreshed = await refreshScene.Handle(refreshSceneCommand, cancellationToken);

                var evaluation = await evaluateEncounters.Handle(
                    new EvaluateEncountersCommand
                    {
                        WorldId = player.WorldId,
                        PlayerId = player.Id,
                    },
                    cancellationToken
                );

                playerLocationId = command.DestinationLocationId;
                startedEncounter = evaluation.Encounter;
            }

            transaction.Complete();
        }

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = command.PlayerId,
                Encounter = startedEncounter,
            },
            cancellationToken
        );

        return new MovePlayerResult(playerLocationId, startedEncounter, refreshed.Scene);
    }

    private async Task MoveTo(
        Creature player,
        Guid fromLocationId,
        Guid toLocationId,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        await updateCreatures.Handle(
            new UpdateCreaturesCommand { CreatureIds = [player.Id], LocationId = toLocationId },
            cancellationToken
        );

        await domainEvents.Publish(
            new PlayerMovedEvent(
                PlayerId: player.Id,
                WorldId: player.WorldId,
                FromLocationId: fromLocationId,
                ToLocationId: toLocationId,
                Playtime: playtime
            ),
            cancellationToken
        );
    }
}
