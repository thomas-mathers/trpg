using System.Diagnostics;

namespace TRPG;

internal record GameSession(Guid WorldId, Guid PlayerId, TimeSpan BankedPlaytime) {
    public Dictionary<string, Guid> ActiveConversationNpcs { get; } = [];
    public TimeSpan BankedPlaytime { get; set; } = BankedPlaytime;
    public bool DidMoveThisTurn { get; set; }
    public Stopwatch SessionStopwatch { get; } = Stopwatch.StartNew();
}