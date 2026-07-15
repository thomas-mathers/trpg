using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class UpdateCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public Optional<Guid?> CityId { get; init; }
    public Optional<Guid?> DistrictId { get; init; }
    public Optional<Guid?> RoomId { get; init; }
    public CreatureState? State { get; init; }
}

internal class UpdateCreaturesCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        UpdateCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        await context
            .Creatures.Where(c => command.CreatureIds.Contains(c.Id))
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(
                            c => c.CityId,
                            c => command.CityId.IsSet ? command.CityId.Value : c.CityId
                        )
                        .SetProperty(
                            c => c.DistrictId,
                            c => command.DistrictId.IsSet ? command.DistrictId.Value : c.DistrictId
                        )
                        .SetProperty(
                            c => c.RoomId,
                            c => command.RoomId.IsSet ? command.RoomId.Value : c.RoomId
                        )
                        .SetProperty(c => c.State, c => command.State ?? c.State),
                cancellationToken
            );
    }
}
