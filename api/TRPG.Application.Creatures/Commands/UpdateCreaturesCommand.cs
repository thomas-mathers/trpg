using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class UpdateCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public Guid? LocationId { get; init; }
    public CreatureState? State { get; init; }
    public TimeSpan? LastRegenPlaytime { get; init; }
}

internal class UpdateCreaturesCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<UpdateCreaturesCommand>
{
    public async Task Handle(
        UpdateCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var hasFieldToUpdate =
            command.LocationId != null
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
                    if (command.LocationId != null)
                    {
                        s.SetProperty(c => c.PreviousLocationId, c => c.LocationId);
                        s.SetProperty(c => c.LocationId, command.LocationId.Value);
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
