using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncLocationRoutinesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncLocationRoutinesCommandHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<SyncWeatherCommand> syncWeather,
    ICommandHandler<SyncLocationJobsCommand> syncLocationJobs,
    ICommandHandler<SyncFrontDoorLockCommand> syncFrontDoorLock,
    ICommandHandler<SyncCreatureSpawnerCommand> syncCreatureSpawner,
    ICommandHandler<SyncRestockPolicyCommand> syncRestockPolicy,
    ICommandHandler<SyncQuestSeedScheduleCommand> syncQuestSeedSchedule
) : ICommandHandler<SyncLocationRoutinesCommand>
{
    public async Task Handle(
        SyncLocationRoutinesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (location == null)
        {
            return;
        }

        // Weather precedes jobs because a creature's routine can be overridden by it.
        await syncWeather.Handle(
            new SyncWeatherCommand
            {
                WorldId = command.WorldId,
                StateId = location.StateId,
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );

        await syncLocationJobs.Handle(
            new SyncLocationJobsCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        await syncFrontDoorLock.Handle(
            new SyncFrontDoorLockCommand
            {
                LocationId = command.LocationId,
                CurrentDate = GameClock.GetCurrentInGameDate(command.GameTime),
            },
            cancellationToken
        );

        await syncCreatureSpawner.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );

        await syncRestockPolicy.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );

        await syncQuestSeedSchedule.Handle(
            new SyncQuestSeedScheduleCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
