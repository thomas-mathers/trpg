using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncWeatherCommand
{
    public required Guid WorldId { get; init; }
    public required Guid StateId { get; init; }
    public required TimeSpan CurrentPlaytime { get; init; }
}

internal class SyncWeatherCommandHandler(ILocationSimulationDbContext context)
    : ICommandHandler<SyncWeatherCommand>
{
    public async Task Handle(
        SyncWeatherCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var season = GameClock.GetCurrentSeason(command.CurrentPlaytime);

        var weatherState = await context.WeatherStates.FirstOrDefaultAsync(
            w => w.StateId == command.StateId,
            cancellationToken
        );

        if (weatherState == null)
        {
            context.WeatherStates.Add(
                new WeatherState
                {
                    WorldId = command.WorldId,
                    StateId = command.StateId,
                    Condition = WeatherRoll.InitialCondition(season),
                    NextChangePlaytime = command.CurrentPlaytime + WeatherRoll.NextChangeOffset(),
                }
            );
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (command.CurrentPlaytime < weatherState.NextChangePlaytime)
        {
            return;
        }

        weatherState.Condition = WeatherRoll.NextCondition(weatherState.Condition, season);
        weatherState.NextChangePlaytime = command.CurrentPlaytime + WeatherRoll.NextChangeOffset();
        await context.SaveChangesAsync(cancellationToken);
    }
}
