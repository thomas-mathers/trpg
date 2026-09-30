using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class ExecutePlayerMoveCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string DestinationName { get; init; }
}

public abstract record ExecutePlayerMoveResult;

public sealed record MoveRejectedResult(EntryOutcome Outcome) : ExecutePlayerMoveResult;

public sealed record MoveTravelDeathResult : ExecutePlayerMoveResult;

public sealed record MoveInterruptedResult(SceneResult Scene, Encounter Encounter)
    : ExecutePlayerMoveResult;

public sealed record MoveCompletedResult(
    SceneResult Scene,
    Encounter? Encounter,
    double TravelTimeHours
) : ExecutePlayerMoveResult;

internal class ExecutePlayerMoveCommandHandler(
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveMoveDestinationCommand,
        ResolveMoveDestinationResult
    > resolveMoveDestination,
    ICommandHandler<
        EvaluateMoveInterceptionCommand,
        EncounterEvaluationResult
    > evaluateMoveInterception,
    ICommandHandler<MovePlayerCommand> movePlayer,
    ICommandHandler<AdvanceTimeCommand, GameInstant> advanceTime,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetSceneQuery, SceneResult> getScene,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
) : ICommandHandler<ExecutePlayerMoveCommand, ExecutePlayerMoveResult>
{
    public async Task<ExecutePlayerMoveResult> Handle(
        ExecutePlayerMoveCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var activeEncounter = await GetActiveEncounter(command.PlayerId, cancellationToken);
        if (activeEncounter != null)
        {
            return new MoveRejectedResult(EntryOutcome.EncounterActive);
        }

        var gameTime = await GetGameTime(command.SessionId, cancellationToken);
        var destination = await ResolveDestination(command, gameTime, cancellationToken);
        if (destination.Outcome != EntryOutcome.Entered)
        {
            return new MoveRejectedResult(destination.Outcome);
        }

        var player = await GetPlayer(command.PlayerId, cancellationToken);
        var destinationLocationId = destination.DestinationLocationId!.Value;
        var interruption = await InterceptMove(
            command,
            player.LocationId,
            destinationLocationId,
            gameTime,
            cancellationToken
        );
        if (interruption != null)
        {
            return interruption;
        }

        return await CompleteMove(
            command,
            destinationLocationId,
            destination.TravelTimeHours,
            gameTime,
            cancellationToken
        );
    }

    private async Task<MoveInterruptedResult?> InterceptMove(
        ExecutePlayerMoveCommand command,
        Guid fromLocationId,
        Guid destinationLocationId,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        await RefreshOrigin(command, gameTime, cancellationToken);
        var encounter = await EvaluateInterception(
            command,
            fromLocationId,
            destinationLocationId,
            gameTime,
            cancellationToken
        );
        if (encounter == null)
        {
            return null;
        }

        var scene = await GetScene(command, gameTime, cancellationToken);
        await PublishEncounter(command.PlayerId, encounter, gameTime, cancellationToken);
        return new MoveInterruptedResult(scene, encounter);
    }

    private async Task<ExecutePlayerMoveResult> CompleteMove(
        ExecutePlayerMoveCommand command,
        Guid destinationLocationId,
        double travelTimeHours,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        var travel = await AdvanceTravel(command, travelTimeHours, gameTime, cancellationToken);
        if (travel.PlayerDied)
        {
            return new MoveTravelDeathResult();
        }

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = command.PlayerId,
                DestinationLocationId = destinationLocationId,
                GameTime = travel.ArrivalGameTime,
            },
            cancellationToken
        );

        var encounter = await GetActiveEncounter(command.PlayerId, cancellationToken);
        await PublishEncounter(
            command.PlayerId,
            encounter,
            travel.ArrivalGameTime,
            cancellationToken
        );
        var scene = await GetScene(command, travel.ArrivalGameTime, cancellationToken);
        return new MoveCompletedResult(scene, encounter, travelTimeHours);
    }

    private async Task<TravelProgress> AdvanceTravel(
        ExecutePlayerMoveCommand command,
        double travelTimeHours,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        if (travelTimeHours <= 0)
        {
            return new TravelProgress(gameTime, false);
        }

        var arrivalGameTime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                WorldId = command.WorldId,
                Delta = TimeSpan.FromHours(1) * travelTimeHours,
            },
            cancellationToken
        );
        var effectVitals = await AdvanceEffects(command, arrivalGameTime, cancellationToken);
        if (effectVitals.HasDied(command.PlayerId))
        {
            return new TravelProgress(arrivalGameTime, true);
        }

        await ApplyRegen(command.PlayerId, arrivalGameTime, cancellationToken);
        return new TravelProgress(arrivalGameTime, false);
    }

    private Task RefreshOrigin(
        ExecutePlayerMoveCommand command,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

    private async Task<Encounter?> EvaluateInterception(
        ExecutePlayerMoveCommand command,
        Guid fromLocationId,
        Guid destinationLocationId,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        var result = await evaluateMoveInterception.Handle(
            new EvaluateMoveInterceptionCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                FromLocationId = fromLocationId,
                ToLocationId = destinationLocationId,
                GameTime = gameTime,
            },
            cancellationToken
        );
        return result.Encounter;
    }

    private Task<IReadOnlyCollection<CreatureVitals>> AdvanceEffects(
        ExecutePlayerMoveCommand command,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        advanceCreatureEffects.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = command.WorldId,
                CreatureIds = [command.PlayerId],
                GameTime = gameTime,
            },
            cancellationToken
        );

    private Task<IReadOnlyDictionary<Guid, Creature>> ApplyRegen(
        Guid playerId,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { GameTime = gameTime, CreatureIds = [playerId] },
            cancellationToken
        );

    private Task<ResolveMoveDestinationResult> ResolveDestination(
        ExecutePlayerMoveCommand command,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        resolveMoveDestination.Handle(
            new ResolveMoveDestinationCommand
            {
                PlayerId = command.PlayerId,
                DestinationName = command.DestinationName,
                GameTime = gameTime,
            },
            cancellationToken
        );

    private async Task<Creature> GetPlayer(Guid playerId, CancellationToken cancellationToken) =>
        await getCreatureById.Handle(new GetCreatureByIdQuery { Id = playerId }, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(Creature), playerId);

    private Task<Encounter?> GetActiveEncounter(
        Guid playerId,
        CancellationToken cancellationToken
    ) =>
        getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = playerId },
            cancellationToken
        );

    private Task<GameInstant> GetGameTime(Guid sessionId, CancellationToken cancellationToken) =>
        getGameTime.Handle(new GetGameTimeQuery { SessionId = sessionId }, cancellationToken);

    private Task<SceneResult> GetScene(
        ExecutePlayerMoveCommand command,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        getScene.Handle(
            new GetSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                CurrentDate = GameClock.GetCurrentInGameDate(gameTime),
                GameTime = gameTime,
            },
            cancellationToken
        );

    private Task PublishEncounter(
        Guid playerId,
        Encounter? encounter,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = playerId,
                Encounter = encounter,
                GameTime = gameTime,
            },
            cancellationToken
        );

    private sealed record TravelProgress(GameInstant ArrivalGameTime, bool PlayerDied);
}
