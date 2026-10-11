using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs;

public static class WeatherShelter
{
    public static CreatureJob Apply(
        CreatureJob job,
        Guid? shelterLocationId,
        IReadOnlySet<Guid>? severeWeatherLocationIds
    ) =>
        shelterLocationId is { } shelter
        && job.Action == CreatureJobAction.Idle
        && severeWeatherLocationIds?.Contains(job.LocationId) == true
            ? AtLocation(job, shelter)
            : job;

    private static CreatureJob AtLocation(CreatureJob job, Guid locationId) =>
        new()
        {
            Id = job.Id,
            Action = job.Action,
            CreatureId = job.CreatureId,
            EndHour = job.EndHour,
            LocationId = locationId,
            Priority = job.Priority,
            SpecificDay = job.SpecificDay,
            StartHour = job.StartHour,
            WorldId = job.WorldId,
        };
}
