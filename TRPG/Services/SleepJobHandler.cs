using TRPG.Data.Models;

namespace TRPG.Services;

internal class SleepJobHandler(CreatureService creatureService)
{
    public async Task Execute(
        Creature creature,
        Job job,
        CancellationToken cancellationToken = default
    )
    {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Sleeping)
        {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Sleeping;
        await creatureService.Update(creature, cancellationToken);
    }
}
