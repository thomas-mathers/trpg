using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Commands;

public class AdvanceTimeCommand
{
    public required Guid WorldId { get; init; }
    public required TimeSpan Delta { get; init; }
}

internal class AdvanceTimeCommandHandler(IWorldClock worldClock)
    : ICommandHandler<AdvanceTimeCommand, GameInstant>
{
    public async Task<GameInstant> Handle(
        AdvanceTimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        return await worldClock.Advance(command.WorldId, command.Delta, cancellationToken);
    }
}
