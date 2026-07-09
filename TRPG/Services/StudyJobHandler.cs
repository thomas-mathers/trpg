using TRPG.Models;

namespace TRPG.Services;

internal class StudyJobHandler(CreatureService creatureService) {
    public async Task Execute(Creature creature, Job job, CancellationToken cancellationToken = default) {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Studying) {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Studying;
        await creatureService.Update(creature, cancellationToken);
    }
}
