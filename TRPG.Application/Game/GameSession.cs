using System.Diagnostics;
using TRPG.Application.Scenes.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Game;

public record GameSession(Guid WorldId, Guid PlayerId, TimeSpan BankedPlaytime)
{
    public Dictionary<string, Guid> ActiveConversationNpcs { get; } = [];
    public TimeSpan BankedPlaytime { get; set; } = BankedPlaytime;
    public bool DidMoveThisTurn { get; set; }
    public bool SceneRefreshedThisTurn { get; set; }
    public Guid? LastCatchUpScopeId { get; set; }
    public InGameDate? LastCatchUpDate { get; set; }
    public SceneResult? LastScene { get; set; }
    public Stopwatch SessionStopwatch { get; } = Stopwatch.StartNew();
}
