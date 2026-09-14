using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncQuestSeedScheduleCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required TimeSpan CurrentPlaytime { get; init; }
}

internal class SyncQuestSeedScheduleCommandHandler(
    ILocationSimulationDbContext context,
    ICommandHandler<SeedCaptiveRescueQuestCommand, bool> seedCaptiveRescueQuest,
    ICommandHandler<SeedClearDungeonQuestCommand, bool> seedClearDungeonQuest
) : ICommandHandler<SyncQuestSeedScheduleCommand>
{
    private const double SeedChance = 0.25;

    public async Task Handle(
        SyncQuestSeedScheduleCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var schedule = await context.QuestSeedSchedules.FirstOrDefaultAsync(
            s => s.LocationId == command.LocationId,
            cancellationToken
        );
        if (schedule == null)
        {
            return;
        }

        var hasTriggered = RecurringScheduling.HasTriggered(
            schedule.Schedule,
            schedule.LastSyncPlaytime,
            command.CurrentPlaytime
        );
        if (!hasTriggered)
        {
            return;
        }

        // Advances the schedule whether or not the roll succeeds, or the next check would fire
        // on every catch-up until it finally hits instead of waiting for the next scheduled window.
        schedule.LastSyncPlaytime = command.CurrentPlaytime;
        await context.SaveChangesAsync(cancellationToken);

        if (Random.Shared.NextDouble() >= SeedChance)
        {
            return;
        }

        await seedCaptiveRescueQuest.Handle(
            new SeedCaptiveRescueQuestCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
            },
            cancellationToken
        );

        await seedClearDungeonQuest.Handle(
            new SeedClearDungeonQuestCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
            },
            cancellationToken
        );
    }
}
