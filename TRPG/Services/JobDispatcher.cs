using Microsoft.Extensions.Logging;
using TRPG.Models;

namespace TRPG.Services;

internal class JobDispatcher(
    SleepJobHandler sleepHandler,
    WorkJobHandler workHandler,
    IdleJobHandler idleHandler,
    ILogger<JobDispatcher> logger
) {
    public async Task Dispatch(Person person, Job job, CancellationToken cancellationToken = default) {
        switch (job.Action) {
            case JobAction.Sleep:
                await sleepHandler.Execute(person, job, cancellationToken);
                break;
            case JobAction.Work:
                await workHandler.Execute(person, job, cancellationToken);
                break;
            case JobAction.Idle:
                await idleHandler.Execute(person, job, cancellationToken);
                break;
            case JobAction.Patrol:
            case JobAction.Socialize:
                logger.LogDebug("[job-dispatcher] {Action} not yet handled, skipping job {JobId}", job.Action, job.Id);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(job), job.Action, "Unhandled JobAction.");
        }
    }
}
