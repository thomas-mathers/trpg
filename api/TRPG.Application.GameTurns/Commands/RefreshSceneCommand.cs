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
    public required TimeSpan Playtime { get; init; }
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

        var playtime = command.Playtime;

        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        var refreshed = await catchUpLocation.Handle(
            new CatchUpLocationCommand
            {
                WorldId = command.WorldId,
                LocationId = player!.LocationId,
                CurrentDate = currentDate,
                PlayerLevel = player.Level,
                Playtime = playtime,
            },
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        return new RefreshSceneResult(scene, refreshed);
    }
}
