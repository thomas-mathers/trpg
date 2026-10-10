using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupyAnyAvailableWorkstationCommand
{
    public required Guid LocationId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class TryOccupyAnyAvailableWorkstationCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupyAnyAvailableWorkstationCommand, Guid?>
{
    public async Task<Guid?> Handle(
        TryOccupyAnyAvailableWorkstationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var excludedWorkstationIds = new HashSet<Guid>();
        while (true)
        {
            var candidateId = await context
                .Props.OfType<Workstation>()
                .Where(workstation =>
                    workstation.LocationId == command.LocationId
                    && workstation.WorkstationType != WorkstationType.Reading
                    && workstation.OccupantId == null
                    && !excludedWorkstationIds.AsEnumerable().Contains(workstation.Id)
                )
                .OrderBy(workstation => workstation.Id)
                .Select(workstation => (Guid?)workstation.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (candidateId is null)
            {
                return null;
            }

            var updated = await context
                .Props.OfType<Workstation>()
                .Where(workstation =>
                    workstation.Id == candidateId && workstation.OccupantId == null
                )
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            workstation => workstation.OccupantId,
                            command.CreatureId
                        ),
                    cancellationToken
                );
            if (updated == 1)
            {
                return candidateId.Value;
            }

            excludedWorkstationIds.Add(candidateId.Value);
        }
    }
}
