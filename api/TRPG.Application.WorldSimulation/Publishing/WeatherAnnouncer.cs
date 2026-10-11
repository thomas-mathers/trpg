using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Weather.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Application.WorldSimulation.Subscriptions;
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
    SceneSubscriptions subscriptions,
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
            var watch = subscriptions.SnapshotOf(worldId, player.PlayerId);
            if (watch is null || watch.LocationId != player.LocationId)
            {
                continue;
            }

            var weather = await CurrentWeather(player.LocationId, cancellationToken);
            if (weather == watch.Weather)
            {
                continue;
            }

            var stamp = await stampWorldState.Handle(
                new StampWorldStateCommand { WorldId = worldId },
                cancellationToken
            );
            scenePublisher.PublishWeather(worldId, player.LocationId, stamp, weather);
            subscriptions.RecordWeather(worldId, player.PlayerId, weather);
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
