using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.Worlds.Commands;

public class SetWorldPlaytimeCommand
{
    public required Guid WorldId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class SetWorldPlaytimeCommandHandler(TrpgDbContext context)
    : ICommandHandler<SetWorldPlaytimeCommand>
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
