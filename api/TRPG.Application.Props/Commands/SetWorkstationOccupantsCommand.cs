using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class SetWorkstationOccupantsCommand
{
    public required IReadOnlyDictionary<Guid, Guid?> OccupantIdsByWorkstationId { get; init; }
}

// Grouped ExecuteUpdateAsync calls, one per distinct occupant value, rather than a tracked
// fetch-mutate-save: ClearWorkstationOccupantsCommand writes via ExecuteUpdateAsync too, and a
// tracked fetch here would silently miss that clear on an entity already tracked from an earlier
// call on the same DbContext, since EF keeps the stale in-memory value instead of the fresh row.
internal class SetWorkstationOccupantsCommandHandler(IPropsDbContext context)
    : ICommandHandler<SetWorkstationOccupantsCommand>
{
    public async Task Handle(
        SetWorkstationOccupantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (
            var group in command.OccupantIdsByWorkstationId.GroupBy(
                entry => entry.Value,
                entry => entry.Key
            )
        )
        {
            var workstationIds = group.ToArray();
            var occupantId = group.Key;
            await context
                .Props.OfType<Workstation>()
                .Where(workstation => workstationIds.AsEnumerable().Contains(workstation.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(workstation => workstation.OccupantId, occupantId),
                    cancellationToken
                );
        }
    }
}
