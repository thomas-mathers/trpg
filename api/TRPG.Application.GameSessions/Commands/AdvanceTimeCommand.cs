using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Commands;

public class AdvanceTimeCommand
{
    public required Guid SessionId { get; init; }
    public required TimeSpan Delta { get; init; }
}

internal class AdvanceTimeCommandHandler(
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ICommandHandler<UpdateGameSessionCommand> updateGameSession
) : ICommandHandler<AdvanceTimeCommand, GameInstant>
{
    public async Task<GameInstant> Handle(
        AdvanceTimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var currentGameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        var gameTime = currentGameTime + command.Delta;
        await updateGameSession.Handle(
            new UpdateGameSessionCommand { SessionId = command.SessionId, GameTime = gameTime },
            cancellationToken
        );

        return gameTime;
    }
}
