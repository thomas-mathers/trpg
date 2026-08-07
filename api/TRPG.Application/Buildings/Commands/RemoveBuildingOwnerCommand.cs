using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Buildings.Commands;

internal class RemoveBuildingOwnerCommand
{
    public required Guid BuildingId { get; init; }
    public required Guid OwnerId { get; init; }
}

internal class RemoveBuildingOwnerCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        RemoveBuildingOwnerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .BuildingOwners.Where(o =>
                o.BuildingId == command.BuildingId && o.OwnerId == command.OwnerId
            )
            .ExecuteDeleteAsync(cancellationToken);
    }
}
