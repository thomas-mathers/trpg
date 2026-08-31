using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class RefreshSceneCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

public record RefreshSceneResult(SceneResult Scene, bool Refreshed);

internal class RefreshSceneCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<SyncSceneCommand> syncScene,
    IQueryHandler<GetSceneQuery, SceneResult> getScene,
    SceneCatchUpCache catchUpCache
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

        var refreshed = await CatchUpScene(
            command.WorldId,
            player!.LocationId,
            currentDate,
            player.Level,
            playtime,
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

    private async Task<bool> CatchUpScene(
        Guid worldId,
        Guid locationId,
        InGameDate currentDate,
        int playerLevel,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        if (catchUpCache.HasCaughtUp(worldId, locationId, currentDate.Hour))
        {
            return false;
        }

        await syncScene.Handle(
            new SyncSceneCommand
            {
                WorldId = worldId,
                LocationId = locationId,
                CurrentDate = currentDate,
                PlayerLevel = playerLevel,
                Playtime = playtime,
            },
            cancellationToken
        );

        catchUpCache.MarkCaughtUp(worldId, locationId, currentDate.Hour);

        return true;
    }
}
