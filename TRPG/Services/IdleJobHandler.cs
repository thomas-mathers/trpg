using TRPG.Models;

namespace TRPG.Services;

internal class IdleJobHandler(PersonService personService) {
    public async Task Execute(Person person, Job job, CancellationToken cancellationToken = default) {
        if (person.RoomId == job.RoomId) return;
        person.RoomId = job.RoomId;
        await personService.Update(person, cancellationToken);
    }
}
