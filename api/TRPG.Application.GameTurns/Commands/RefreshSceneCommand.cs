using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class RefreshSceneCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
}

public record RefreshSceneResult(SceneResult Scene, bool Refreshed);

internal class RefreshSceneCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<CatchUpLocationCommand, bool> catchUpLocation,
    IQueryHandler<GetSceneQuery, SceneResult> getScene
) : ICommandHandler<RefreshSceneCommand, RefreshSceneResult>
{
    public async Task<RefreshSceneResult> Handle(
        RefreshSceneCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var gameTime = command.GameTime;

        var currentDate = GameClock.GetCurrentInGameDate(gameTime);

        var refreshed = await catchUpLocation.Handle(
            new CatchUpLocationCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = player!.LocationId,
                CurrentDate = currentDate,
                PlayerLevel = player.Level,
                GameTime = gameTime,
            },
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                CurrentDate = currentDate,
                GameTime = gameTime,
            },
            cancellationToken
        );

        return new RefreshSceneResult(scene, refreshed);
    }
}
