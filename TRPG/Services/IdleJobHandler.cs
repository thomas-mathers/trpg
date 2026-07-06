using TRPG.Models;

namespace TRPG.Services;

internal class IdleJobHandler(CreatureService creatureService) {
    public async Task Execute(Creature creature, Job job, CancellationToken cancellationToken = default) {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Idle) {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Idle;
        await creatureService.Update(creature, cancellationToken);
    }
}
