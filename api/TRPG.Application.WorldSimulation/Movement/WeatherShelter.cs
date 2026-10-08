using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

internal sealed class WeatherShelter
{
    private IReadOnlySet<Guid> _exposedLocationIds = new HashSet<Guid>();

    public bool Update(IReadOnlySet<Guid> exposedLocationIds)
    {
        if (_exposedLocationIds.SetEquals(exposedLocationIds))
        {
            return false;
        }

        _exposedLocationIds = exposedLocationIds;
        return true;
    }

    public CreatureJob Apply(SimulatedCreature creature, CreatureJob job) =>
        creature.ShelterLocationId is { } shelter
        && job.Action == CreatureJobAction.Idle
        && _exposedLocationIds.Contains(job.LocationId)
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
            RouteId = job.RouteId,
            SpecificDay = job.SpecificDay,
            StartHour = job.StartHour,
            WorldId = job.WorldId,
        };
}
