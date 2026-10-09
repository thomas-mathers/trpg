using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class WorkstationAssigner
{
    internal static void Assign(IEnumerable<CreatureJob> jobs, IEnumerable<Prop> props)
    {
        var workstationsByLocation = props
            .OfType<Workstation>()
            .ToLookup(workstation => workstation.LocationId);

        foreach (var job in jobs.Where(job => job.Action == CreatureJobAction.Work))
        {
            var workstation = workstationsByLocation[job.LocationId]
                .FirstOrDefault(candidate => candidate.AssignedCreatureId == job.CreatureId);
            workstation ??= workstationsByLocation[job.LocationId]
                .FirstOrDefault(candidate => candidate.AssignedCreatureId is null);

            if (workstation is null)
            {
                throw new InvalidOperationException(
                    "Every work location must provide a workstation."
                );
            }

            workstation.AssignedCreatureId = job.CreatureId;
        }
    }
}
