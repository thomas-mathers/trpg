namespace TRPG.Services;

internal class JobCatchUpService(JobService jobService, PersonService personService, JobDispatcher dispatcher) {
    public async Task CatchUpRoom(Guid roomId, int hour, CancellationToken cancellationToken = default) {
        var personIds = await jobService.GetPersonIdsByRoomId(roomId, cancellationToken);
        await CatchUp(personIds, hour, cancellationToken);
    }

    public async Task CatchUpDistrict(Guid worldId, Guid districtId, int hour,
        CancellationToken cancellationToken = default) {
        var personIds = await personService.GetIdsByDistrict(worldId, districtId, cancellationToken);
        await CatchUp(personIds, hour, cancellationToken);
    }

    private async Task CatchUp(IReadOnlyCollection<Guid> personIds, int hour, CancellationToken cancellationToken) {
        foreach (var personId in personIds) {
            var jobs = await jobService.GetAllByPersonId(personId, cancellationToken);

            var dueJob = jobs
                .Where(j => JobScheduling.IsActiveAtHour(j, hour))
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Id)
                .FirstOrDefault();

            if (dueJob == null) {
                continue;
            }

            var person = await personService.GetById(personId, cancellationToken);

            if (person != null) {
                await dispatcher.Dispatch(person, dueJob, cancellationToken);
            }
        }
    }
}