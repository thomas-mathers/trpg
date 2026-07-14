using TRPG.Application.Game;

namespace TRPG;

internal class CurrentGameSessionStateAccessor
{
    public GameSessionState State { get; set; } = null!;
}
