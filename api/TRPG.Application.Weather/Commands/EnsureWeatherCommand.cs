using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Weather.Commands;

public class EnsureWeatherCommand
{
    public required Guid WorldId { get; init; }
    public required Guid StateId { get; init; }
    public required GameInstant CurrentGameTime { get; init; }
}

internal class EnsureWeatherCommandHandler(IWeatherDbContext context)
    : ICommandHandler<EnsureWeatherCommand>
{
    public async Task Handle(
        EnsureWeatherCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await context.WeatherStates.AnyAsync(
            w => w.StateId == command.StateId,
            cancellationToken
        );
        if (exists)
        {
            return;
        }

        context.WeatherStates.Add(
            new WeatherState
            {
                WorldId = command.WorldId,
                StateId = command.StateId,
                Condition = WeatherRoll.InitialCondition(
                    GameClock.GetCurrentSeason(command.CurrentGameTime)
                ),
                NextChangeGameTime = command.CurrentGameTime + WeatherRoll.NextChangeOffset(),
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
