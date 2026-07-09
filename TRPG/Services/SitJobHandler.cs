using TRPG.Models;

namespace TRPG.Services;

internal class SitJobHandler(CreatureService creatureService) {
    public async Task Execute(Creature creature, Job job, CancellationToken cancellationToken = default) {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Sitting) {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Sitting;
        await creatureService.Update(creature, cancellationToken);
    }
}
