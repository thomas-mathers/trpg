using TRPG.Data.Models;

namespace TRPG.Application.Game;

public class TurnStreamResult
{
    public bool DidSceneRefreshThisTurn { get; set; }
    public InGameDate CurrentDate { get; set; }
}
