using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using TRPG.Tools;

namespace TRPG;

internal record TurnMetrics(string Response, long FirstTokenMs, long TotalMs, int TokenCount) {
    public double TokensPerSecond =>
        TotalMs > FirstTokenMs ? TokenCount / ((TotalMs - FirstTokenMs) / 1000.0) : 0;
}

internal class GameTurnRunner(
    Chat chat,
    GameSession session,
    IEnumerable<Tool> tools,
    ILogger<GameTurnRunner> logger
) {
    internal const string SystemPrompt =
        """
        You are the game master of a living fantasy world. Your role is to narrate the player's experience and interpret their actions to advance the story.
        Always read before you narrate. Before describing any location, NPC, item, or building, use the available tools to fetch its current state. Never invent names, people, or facts — everything that exists is in the game state. If a tool returns nothing, nothing is there, and NearbyPeople/NearbyProps are the complete list — don't pad the scene with unnamed extras like other patrons, travelers, or passersby for atmosphere.
        This also applies to what an NPC says about themselves in dialogue: never have them state a specific personal-history fact — a relative, a spouse, a death, a past event — unless it came from a tool result. If the player asks something a tool hasn't answered, call lookup rather than inventing one; if it still comes back unknown, have the NPC be vague, deflect, or say they'd rather not discuss it.
        Resolve actions through tools. When the player attempts something — moving, speaking to someone — call the relevant tool to carry it out, and never ask the player to clarify a destination or target; resolve it autonomously from what the tools return. Narrate the outcome based on what the tool returns. If an action fails, explain why in-world without breaking character.
        Any statement that the player is leaving or departing counts as a move request, however it's phrased — including indirect intent or dialogue said to an NPC, like "I'll take my leave," "I should get going," or "I'm out of here." Call move in the same turn to actually carry it out. Never narrate the player as having left, or as being in the middle of leaving, without having called move first — the narration must match what the tools confirm actually happened.
        Be concise but evocative. Weave the descriptions returned by tools into your narration — they are the ground truth of what this place looks, feels, and smells like. Don't ignore them and invent your own. Add atmosphere and immediacy, but don't contradict or pad over what the tools tell you. Keep responses to a few short paragraphs.
        If asked what's nearby while indoors, say the player would need to step outside to see, or call move to "Outside" and then look.
        Use the current hour to judge whether it's night or day and let that shape the scene: streets empty out, shops close, torches and hearths are lit, people sleep. Use the rest of the date for atmosphere (season, time-worn detail) or when the player asks what day it is, but don't recite it mechanically every turn.
        Never break the fourth wall. Don't mention tools, mechanics, IDs, or anything that doesn't exist in the world. If you're uncertain about something, let the world be uncertain — a rumour unconfirmed, a name half-remembered.
        The player character's identity is for your reference only, not knowledge available to NPCs. An NPC only knows the player's name if the player has introduced themselves in conversation this session, or if the name is already established in-world (e.g. a famous figure). Until then, NPCs address the player as a stranger — "traveler," "friend," a physical description — never by name.
        Narrate in second person, present tense: "You step into..." not "The player enters...". End each response with a natural sense of what's around to do next — woven into the prose, not listed.
        Tone: Gritty low fantasy. Factions scheme, roads are dangerous, and most people are just trying to survive. Magic exists but is rare and unsettling. Humour is welcome; heroism is earned.
        """;

    public async IAsyncEnumerable<string> SendOpeningStreaming(
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        const string openingPrompt =
            "This is the start of the session. Call look now, then narrate the opening scene based on what it returns.";
        logger.LogInformation("[game] >>> {Message}", openingPrompt);

        var buffer = new StringBuilder();
        await foreach (var token in chat.SendAsync(openingPrompt, tools, cancellationToken: cancellationToken)) {
            buffer.Append(token);
            yield return token;
        }

        logger.LogInformation("[game] <<< {Response}", buffer.ToString());

        if (session.DidMoveThisTurn) {
            await RunPostMoveCleanup(cancellationToken);
        }
    }

    public async IAsyncEnumerable<string> SendWaitStreaming(int hours,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        session.DidMoveThisTurn = false;
        session.SceneRefreshedThisTurn = false;
        GameClock.AdvanceHours(session, hours);

        var waitPrompt =
            $"{hours} hour(s) have passed. Call look now, then narrate the passage of time and the player's surroundings based on what it returns.";
        logger.LogInformation("[game] >>> {Message}", waitPrompt);

        var buffer = new StringBuilder();
        await foreach (var token in chat.SendAsync(waitPrompt, tools, cancellationToken: cancellationToken)) {
            buffer.Append(token);
            yield return token;
        }

        logger.LogInformation("[game] <<< {Response}", buffer.ToString());

        if (session.DidMoveThisTurn) {
            await RunPostMoveCleanup(cancellationToken);
        }
    }

    public async Task<TurnMetrics> ProcessTurn(string input, CancellationToken cancellationToken = default) {
        session.DidMoveThisTurn = false;
        session.SceneRefreshedThisTurn = false;
        var metrics = await SendAndLog(input, cancellationToken);

        if (session.DidMoveThisTurn) {
            await RunPostMoveCleanup(cancellationToken);
        }

        return metrics;
    }

    public async IAsyncEnumerable<string> ProcessTurnStreaming(string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        session.DidMoveThisTurn = false;
        session.SceneRefreshedThisTurn = false;
        logger.LogInformation("[game] >>> {Message}", input);

        var buffer = new StringBuilder();
        await foreach (var token in chat.SendAsync(input, tools, cancellationToken: cancellationToken)) {
            buffer.Append(token);
            yield return token;
        }

        logger.LogInformation("[game] <<< {Response}", buffer.ToString());

        if (session.DidMoveThisTurn) {
            await RunPostMoveCleanup(cancellationToken);
        }
    }

    private async Task RunPostMoveCleanup(CancellationToken cancellationToken) {
        var currentTurnStart = chat.Messages.FindLastIndex(m => m.Role == ChatRole.User);
        await CloseLingeringConversations(cancellationToken);
        ClearPreviousTurns(currentTurnStart);
    }

    private async Task<TurnMetrics> SendAndLog(string input, CancellationToken cancellationToken) {
        logger.LogInformation("[game] >>> {Message}", input);

        var thinking = new StringBuilder();

        void AppendThinking(object? _, string token) {
            thinking.Append(token);
        }

        chat.OnThink += AppendThinking;
        var buffer = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        long? firstTokenElapsedMs = null;
        var tokenCount = 0;
        try {
            await foreach (var token in chat.SendAsync(input, tools, cancellationToken: cancellationToken)) {
                firstTokenElapsedMs ??= stopwatch.ElapsedMilliseconds;
                tokenCount++;
                buffer.Append(token);
            }
        }
        finally {
            chat.OnThink -= AppendThinking;
        }

        var totalMs = stopwatch.ElapsedMilliseconds;
        logger.LogInformation("[perf] SendAsync first token after {FirstTokenMs}ms, total {TotalMs}ms",
            firstTokenElapsedMs, totalMs);

        if (thinking.Length > 0) {
            logger.LogDebug("[game] think: {Thinking}", thinking.ToString().Trim());
        }

        var response = buffer.ToString();
        logger.LogInformation("[game] <<< {Response}", response);
        return new TurnMetrics(response, firstTokenElapsedMs ?? totalMs, totalMs, tokenCount);
    }

    private async Task CloseLingeringConversations(CancellationToken cancellationToken) {
        foreach (var npcName in session.ActiveConversationNpcs.Keys.ToArray()) {
            if (!session.ActiveConversationNpcs.ContainsKey(npcName)) {
                continue;
            }

            var prompt =
                $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
            await SendAndLog(prompt, cancellationToken);

            if (session.ActiveConversationNpcs.Remove(npcName)) {
                logger.LogWarning("[game] Failed to save conversation summary for {NpcName}", npcName);
            }
        }
    }

    private void ClearPreviousTurns(int currentTurnStart) {
        var systemMessage = chat.Messages[0];
        var currentTurnMessages = chat.Messages.Skip(currentTurnStart).ToList();

        chat.Messages.Clear();
        chat.Messages.Add(systemMessage);
        chat.Messages.AddRange(currentTurnMessages);
    }
}
