using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;

namespace TRPG.Application.GameSessions.Commands;

public class AdvanceTimeCommand
{
    public required Guid SessionId { get; init; }
    public required TimeSpan Delta { get; init; }
}

internal class AdvanceTimeCommandHandler(
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<UpdateGameSessionCommand> updateGameSession
) : ICommandHandler<AdvanceTimeCommand, TimeSpan>
{
    public async Task<TimeSpan> Handle(
        AdvanceTimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var currentPlaytime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        var playtime = currentPlaytime + command.Delta;
        await updateGameSession.Handle(
            new UpdateGameSessionCommand { SessionId = command.SessionId, Playtime = playtime },
            cancellationToken
        );

        return playtime;
    }
}
