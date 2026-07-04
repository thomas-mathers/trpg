using TRPG.Models;

namespace TRPG.Services;

internal class WorkJobHandler(PersonService personService) {
    public async Task Execute(Person person, Job job, CancellationToken cancellationToken = default) {
        if (person.RoomId == job.RoomId && person.State == PersonState.Busy) return;
        person.RoomId = job.RoomId;
        person.State = PersonState.Busy;
        await personService.Update(person, cancellationToken);
    }
}
