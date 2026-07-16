using TRPG.Application.Game.Queries;

namespace TRPG.Application.Game.Commands;

internal class AdvanceTimeCommand
{
    public required GameSessionLock Lock { get; init; }
    public required TimeSpan Delta { get; init; }
}

internal class AdvanceTimeCommandHandler(
    GetPlaytimeQueryHandler getPlaytime,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public async Task<TimeSpan> Handle(
        AdvanceTimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var currentPlaytime = await getPlaytime.Handle(
            new GetPlaytimeQuery { Lock = command.Lock },
            cancellationToken
        );
        var playtime = currentPlaytime + command.Delta;
        await updateGameSession.Handle(
            new UpdateGameSessionCommand { Lock = command.Lock, Playtime = playtime },
            cancellationToken
        );

        return playtime;
    }
}
