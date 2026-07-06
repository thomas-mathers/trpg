using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TRPG.Services;

internal class JobCatchUpService(
    JobService jobService, CreatureService creatureService, JobDispatcher dispatcher, ILogger<JobCatchUpService> logger
) {
    public async Task CatchUpRoom(Guid roomId, int hour, CancellationToken cancellationToken = default) {
        var creatureIds = await jobService.GetCreatureIdsByRoomId(roomId, cancellationToken);
        await CatchUp("Room", creatureIds, hour, cancellationToken);
    }

    public async Task CatchUpDistrict(Guid worldId, Guid districtId, int hour,
        CancellationToken cancellationToken = default) {
        var creatureIds = await creatureService.GetIdsByDistrict(worldId, districtId, cancellationToken);
        await CatchUp("District", creatureIds, hour, cancellationToken);
    }

    private async Task CatchUp(string scope, IReadOnlyCollection<Guid> creatureIds, int hour,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();

        foreach (var creatureId in creatureIds) {
            var jobs = await jobService.GetAllByCreatureId(creatureId, cancellationToken);

            var dueJob = jobs
                .Where(j => JobScheduling.IsActiveAtHour(j, hour))
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Id)
                .FirstOrDefault();

            if (dueJob == null) {
                continue;
            }

            var creature = await creatureService.GetById(creatureId, cancellationToken);

            if (creature != null) {
                await dispatcher.Dispatch(creature, dueJob, cancellationToken);
            }
        }

        stopwatch.Stop();
        logger.LogInformation("[perf] CatchUp{Scope} processed {CreatureCount} people in {ElapsedMs}ms",
            scope, creatureIds.Count, stopwatch.ElapsedMilliseconds);
    }
}
