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
    public required GameInstant CurrentGameTime { get; init; }
}

internal class SyncWeatherCommandHandler(ILocationSimulationDbContext context)
    : ICommandHandler<SyncWeatherCommand>
{
    public async Task Handle(
        SyncWeatherCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var season = GameClock.GetCurrentSeason(command.CurrentGameTime);

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
                    NextChangeGameTime = command.CurrentGameTime + WeatherRoll.NextChangeOffset(),
                }
            );
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (command.CurrentGameTime < weatherState.NextChangeGameTime)
        {
            return;
        }

        weatherState.Condition = WeatherRoll.NextCondition(weatherState.Condition, season);
        weatherState.NextChangeGameTime = command.CurrentGameTime + WeatherRoll.NextChangeOffset();
        await context.SaveChangesAsync(cancellationToken);
    }
}
