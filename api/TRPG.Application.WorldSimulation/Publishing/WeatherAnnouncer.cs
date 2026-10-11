using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Weather.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class WeatherAnnouncer(
    IQueryHandler<
        GetActiveLocationPlayersQuery,
        IReadOnlyCollection<ActiveLocationPlayer>
    > getActiveLocationPlayers,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeatherByStateId,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    PublishedSceneRegistry publishedScenes,
    ScenePublisher scenePublisher
)
{
    public async Task Announce(Guid worldId, CancellationToken cancellationToken)
    {
        var players = await getActiveLocationPlayers.Handle(
            new GetActiveLocationPlayersQuery { WorldId = worldId },
            cancellationToken
        );
        foreach (var player in players)
        {
            var previous = publishedScenes.Find(player.PlayerId);
            if (previous is null || previous.LocationId != player.LocationId)
            {
                continue;
            }

            var weather = await CurrentWeather(player.LocationId, cancellationToken);
            if (weather == previous.Weather)
            {
                continue;
            }

            var stamp = await stampWorldState.Handle(
                new StampWorldStateCommand { WorldId = worldId },
                cancellationToken
            );
            scenePublisher.PublishWeather(player.PlayerId, weather, stamp);
        }
    }

    private async Task<WeatherCondition?> CurrentWeather(
        Guid locationId,
        CancellationToken cancellationToken
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = locationId },
            cancellationToken
        );
        if (location is null || location.RoomId is not null)
        {
            return null;
        }

        return await getWeatherByStateId.Handle(
            new GetWeatherByStateIdQuery { StateId = location.StateId },
            cancellationToken
        );
    }
}
