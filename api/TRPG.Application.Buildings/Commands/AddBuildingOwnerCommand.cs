using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Commands;

public class AddBuildingOwnerCommand
{
    public required Guid BuildingId { get; init; }
    public required Guid OwnerId { get; init; }
}

public class AddBuildingOwnerCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddBuildingOwnerCommand>
{
    public async Task Handle(
        AddBuildingOwnerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var worldId = await context
            .Buildings.Where(b => b.Id == command.BuildingId)
            .Select(b => b.WorldId)
            .FirstAsync(cancellationToken);

        context.BuildingOwners.Add(
            new BuildingOwner
            {
                Id = Guid.NewGuid(),
                BuildingId = command.BuildingId,
                OwnerId = command.OwnerId,
                WorldId = worldId,
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
