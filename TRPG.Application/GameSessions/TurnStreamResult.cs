using TRPG.Data.Models;

namespace TRPG.Application.GameSessions;

public class TurnStreamResult
{
    public bool DidSceneRefreshThisTurn { get; set; }
    public InGameDate CurrentDate { get; set; }
}
