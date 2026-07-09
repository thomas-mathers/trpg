using TRPG.Models;

namespace TRPG.Services;

internal class WorkJobHandler(CreatureService creatureService)
{
    public async Task Execute(
        Creature creature,
        Job job,
        CancellationToken cancellationToken = default
    )
    {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Busy)
        {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Busy;
        await creatureService.Update(creature, cancellationToken);
    }
}
