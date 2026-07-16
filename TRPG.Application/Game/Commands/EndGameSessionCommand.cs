using TRPG.Application.Game.Queries;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Game.Commands;

internal class EndGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class EndGameSessionCommandHandler(
    SetWorldPlaytimeCommandHandler setWorldPlaytime,
    GetGameSessionQueryHandler getGameSession,
    DeleteGameSessionCommandHandler deleteGameSession
)
{
    public async Task Handle(
        EndGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );

        await setWorldPlaytime.Handle(
            new SetWorldPlaytimeCommand
            {
                WorldId = snapshot.WorldId,
                Playtime = snapshot.Playtime,
            },
            cancellationToken
        );

        await deleteGameSession.Handle(
            new DeleteGameSessionCommand { SessionId = command.SessionId },
            cancellationToken
        );
    }
}
