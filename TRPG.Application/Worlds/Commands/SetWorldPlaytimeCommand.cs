using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Worlds.Commands;

public class SetWorldPlaytimeCommand
{
    public required Guid WorldId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

public class SetWorldPlaytimeCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        SetWorldPlaytimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Worlds.Where(w => w.Id == command.WorldId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(w => w.Playtime, command.Playtime),
                cancellationToken
            );
    }
}
