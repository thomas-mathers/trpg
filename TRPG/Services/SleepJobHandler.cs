using TRPG.Models;

namespace TRPG.Services;

internal class SleepJobHandler(PersonService personService) {
    public async Task Execute(Person person, Job job, CancellationToken cancellationToken = default) {
        if (person.RoomId == job.RoomId && person.State == PersonState.Sleeping) {
            return;
        }

        person.RoomId = job.RoomId;
        person.State = PersonState.Sleeping;
        await personService.Update(person, cancellationToken);
    }
}