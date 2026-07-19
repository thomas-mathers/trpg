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
    public TimeSpan? LastRegenPlaytime { get; init; }
}

internal class UpdateCreaturesCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        UpdateCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var hasFieldToUpdate =
            command.CityId.IsSet
            || command.DistrictId.IsSet
            || command.RoomId.IsSet
            || command.State != null
            || command.LastRegenPlaytime != null;

        if (command.CreatureIds.Count == 0 || !hasFieldToUpdate)
        {
            return;
        }

        await context
            .Creatures.Where(c => command.CreatureIds.Contains(c.Id))
            .ExecuteUpdateAsync(
                s =>
                {
                    if (command.CityId.IsSet)
                    {
                        s.SetProperty(c => c.CityId, command.CityId.Value);
                    }
                    if (command.DistrictId.IsSet)
                    {
                        s.SetProperty(c => c.DistrictId, command.DistrictId.Value);
                    }
                    if (command.RoomId.IsSet)
                    {
                        s.SetProperty(c => c.RoomId, command.RoomId.Value);
                    }
                    if (command.State != null)
                    {
                        s.SetProperty(c => c.State, command.State.Value);
                    }
                    if (command.LastRegenPlaytime != null)
                    {
                        s.SetProperty(c => c.LastRegenPlaytime, command.LastRegenPlaytime.Value);
                    }
                },
                cancellationToken
            );
    }
}
