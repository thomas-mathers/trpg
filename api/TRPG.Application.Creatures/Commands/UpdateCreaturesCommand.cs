using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class UpdateCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public Guid? LocationId { get; init; }
    public CreatureState? State { get; init; }
    public GameInstant? LastRegenGameTime { get; init; }
    public string? Name { get; init; }
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
            || command.LastRegenGameTime != null
            || command.Name != null;

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
                    if (command.LastRegenGameTime != null)
                    {
                        s.SetProperty(c => c.LastRegenGameTime, command.LastRegenGameTime.Value);
                    }
                    if (command.Name != null)
                    {
                        s.SetProperty(c => c.Name, command.Name);
                    }
                },
                cancellationToken
            );
    }
}
