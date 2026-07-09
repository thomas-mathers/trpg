using Microsoft.Extensions.Logging;
using TRPG.Models;

namespace TRPG.Services;

internal class JobDispatcher(
    SleepJobHandler sleepHandler,
    WorkJobHandler workHandler,
    IdleJobHandler idleHandler,
    StudyJobHandler studyHandler,
    PrayJobHandler prayHandler,
    TrainJobHandler trainHandler,
    SitJobHandler sitHandler,
    ILogger<JobDispatcher> logger
)
{
    public async Task Dispatch(
        Creature creature,
        Job job,
        CancellationToken cancellationToken = default
    )
    {
        switch (job.Action)
        {
            case JobAction.Sleep:
                await sleepHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Work:
                await workHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Idle:
                await idleHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Study:
                await studyHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Pray:
                await prayHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Train:
                await trainHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Sit:
                await sitHandler.Execute(creature, job, cancellationToken);
                break;
            case JobAction.Patrol:
            case JobAction.Socialize:
                logger.LogDebug(
                    "[job-dispatcher] {Action} not yet handled, skipping job {JobId}",
                    job.Action,
                    job.Id
                );
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(job),
                    job.Action,
                    "Unhandled JobAction."
                );
        }
    }
}
