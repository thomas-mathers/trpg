using System.Diagnostics;
using OllamaSharp;

namespace TRPG;

internal record GameSession(Guid WorldId, Guid PlayerId, TimeSpan BankedPlaytime) {
    public TimeSpan BankedPlaytime { get; set; } = BankedPlaytime;
    public Dictionary<string, Guid> ActiveConversationNpcs { get; } = [];
    public bool DidMoveThisTurn { get; set; }
    public Stopwatch SessionStopwatch { get; } = Stopwatch.StartNew();
}
