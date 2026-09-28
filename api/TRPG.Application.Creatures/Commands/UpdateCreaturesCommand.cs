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
    public GameInstant? LastRegenGameTime { get; init; }
    public string? Name { get; init; }
    public bool? IsRestrained { get; init; }
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
            || command.LastRegenGameTime != null
            || command.Name != null
            || command.IsRestrained != null;

        if (command.CreatureIds.Count == 0 || !hasFieldToUpdate)
        {
            return;
        }

        var locationId = command.LocationId ?? Guid.Empty;
        var lastRegenGameTime = command.LastRegenGameTime ?? default;
        var isRestrained = command.IsRestrained ?? default;
        var hasLocation = command.LocationId != null;
        var hasLastRegenGameTime = command.LastRegenGameTime != null;
        var hasName = command.Name != null;
        var hasIsRestrained = command.IsRestrained != null;

        // Rows that already hold the requested values are left alone, so repeating an update is not a write.
        await context
            .Creatures.Where(c => command.CreatureIds.Contains(c.Id))
            .Where(c =>
                (hasLocation && c.LocationId != locationId)
                || (hasLastRegenGameTime && c.LastRegenGameTime != lastRegenGameTime)
                || (hasName && c.Name != command.Name)
                || (hasIsRestrained && c.IsRestrained != isRestrained)
            )
            .ExecuteUpdateAsync(
                s =>
                {
                    if (command.LocationId != null)
                    {
                        s.SetProperty(
                            c => c.PreviousLocationId,
                            c => c.LocationId != locationId ? c.LocationId : c.PreviousLocationId
                        );
                        s.SetProperty(c => c.LocationId, command.LocationId.Value);
                    }
                    if (command.LastRegenGameTime != null)
                    {
                        s.SetProperty(c => c.LastRegenGameTime, command.LastRegenGameTime.Value);
                    }
                    if (command.Name != null)
                    {
                        s.SetProperty(c => c.Name, command.Name);
                    }
                    if (command.IsRestrained != null)
                    {
                        s.SetProperty(c => c.IsRestrained, command.IsRestrained.Value);
                    }
                },
                cancellationToken
            );
    }
}
