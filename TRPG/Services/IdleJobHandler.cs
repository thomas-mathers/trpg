using TRPG.Models;

namespace TRPG.Services;

internal class IdleJobHandler(PersonService personService) {
    public async Task Execute(Person person, Job job, CancellationToken cancellationToken = default) {
        if (person.RoomId == job.RoomId && person.State == PersonState.Idle) return;
        person.RoomId = job.RoomId;
        person.State = PersonState.Idle;
        await personService.Update(person, cancellationToken);
    }
}
