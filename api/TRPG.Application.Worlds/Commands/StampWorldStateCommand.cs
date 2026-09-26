using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Worlds.Commands;

public class StampWorldStateCommand
{
    public required Guid WorldId { get; init; }
}

public record WorldStateStamp(long Version, GameInstant GameTime, DateTimeOffset CapturedAt);

internal class StampWorldStateCommandHandler(
    IWorldsDbContext context,
    IWorldClock worldClock,
    TimeProvider timeProvider
) : ICommandHandler<StampWorldStateCommand, WorldStateStamp>
{
    public async Task<WorldStateStamp> Handle(
        StampWorldStateCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var version = await AdvanceVersion(command.WorldId, cancellationToken);

        var gameTime = await worldClock.GetCurrent(command.WorldId, cancellationToken);
        var capturedAt = timeProvider.GetUtcNow().ToUniversalTime();

        return new WorldStateStamp(version, gameTime, capturedAt);
    }

    // Compare-and-swap keeps concurrent stampers from ever receiving the same version.
    private async Task<long> AdvanceVersion(Guid worldId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var current = await context
                .Worlds.AsNoTracking()
                .Where(world => world.Id == worldId)
                .Select(world => (long?)world.StateVersion)
                .SingleOrDefaultAsync(cancellationToken);
            if (current == null)
            {
                throw new EntityNotFoundException("World", worldId);
            }

            var next = current.Value + 1;
            var updated = await context
                .Worlds.Where(world => world.Id == worldId && world.StateVersion == current)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(world => world.StateVersion, next),
                    cancellationToken
                );
            if (updated == 1)
            {
                return next;
            }
        }
    }
}
