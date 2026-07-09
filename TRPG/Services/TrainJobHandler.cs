using TRPG.Models;

namespace TRPG.Services;

internal class TrainJobHandler(CreatureService creatureService) {
    public async Task Execute(Creature creature, Job job, CancellationToken cancellationToken = default) {
        if (creature.RoomId == job.RoomId && creature.State == CreatureState.Training) {
            return;
        }

        creature.RoomId = job.RoomId;
        creature.State = CreatureState.Training;
        await creatureService.Update(creature, cancellationToken);
    }
}
