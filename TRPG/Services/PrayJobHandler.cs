using TRPG.Models;

namespace TRPG.Services;

internal class PrayJobHandler(CreatureService creatureService)
{
    public async Task Execute(
        Creature creature,
        Job job,
        CancellationToken cancellationToken = default
    )
    {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Praying)
        {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Praying;
        await creatureService.Update(creature, cancellationToken);
    }
}
