using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common;
using TRPG.Data.Models;

namespace TRPG.Application.Game;

public record TurnMetrics(string Response, long FirstTokenMs, long TotalMs, int TokenCount)
{
    public double TokensPerSecond =>
        TotalMs > FirstTokenMs ? TokenCount / ((TotalMs - FirstTokenMs) / 1000.0) : 0;
}

public class GameTurnRunner(
    [FromKeyedServices(LlmRoleKeys.Gameplay)] IChatClient chatClient,
    GameSessionState sessionState,
    IEnumerable<AIFunction> tools,
    IOptionsMonitor<LlmRoleOptions> optionsMonitor,
    IOptionsSnapshot<GameClockOptions> gameClockOptions,
    ILogger<GameTurnRunner> logger
)
{
    public const string SystemPrompt = """
        You are the game master of a living fantasy world. Your role is to narrate the player's experience and interpret their actions to advance the story.
        Always read before you narrate. Before describing any location, NPC, item, or building, use the available tools to fetch its current state. Never invent names, people, or facts — everything that exists is in the game state. If a tool returns nothing, nothing is there, and NearbyPeople/NearbyProps are the complete list — don't pad the scene with unnamed extras like other patrons, travelers, or passersby for atmosphere.
        This also applies to what an NPC says about themselves in dialogue: start_conversation's biography is the complete record of that NPC's family, hometown, faction ties, workplace (including whether they own it or just work there), work hours, days off, and home — if it doesn't mention a spouse, parent, child, sibling, employer, or residence, that detail doesn't exist (they're unemployed, or the biography just doesn't cover it), so have them say so plainly or deflect (whichever fits their personality) rather than inventing one. Whether an NPC owns their workplace is stated explicitly in the biography ("they own X" vs "they work at X") — never hedge or guess about this, state it as given. Never state a specific personal-history fact — a relative, a spouse, a death, a past event, a coworker, a specific shift time — unless it came from a tool result. For anything else a tool hasn't covered, call lookup rather than inventing it; if it still comes back unknown, have the NPC be vague, deflect, or say they'd rather not discuss it.
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        const string openingPrompt =
            "This is the start of the session. Call look now, then narrate the opening scene based on what it returns.";

        await sessionState.TurnLock.WaitAsync(cancellationToken);
        try
        {
            await foreach (var token in StreamAndLog(openingPrompt, cancellationToken))
            {
                yield return token;
            }

            if (sessionState.Session.DidMoveThisTurn)
            {
                await RunPostMoveCleanup(cancellationToken);
            }
        }
        finally
        {
            sessionState.TurnLock.Release();
        }
    }

    public async IAsyncEnumerable<string> SendWaitStreaming(
        int hours,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await sessionState.TurnLock.WaitAsync(cancellationToken);
        try
        {
            sessionState.Session.DidMoveThisTurn = false;
            sessionState.Session.DidSceneRefreshThisTurn = false;
            GameClock.AdvanceHours(sessionState.Session, hours);

            var waitPrompt =
                $"{hours} hour(s) have passed. Call look now, then narrate the passage of time and the player's surroundings based on what it returns.";

            await foreach (var token in StreamAndLog(waitPrompt, cancellationToken))
            {
                yield return token;
            }

            if (sessionState.Session.DidMoveThisTurn)
            {
                await RunPostMoveCleanup(cancellationToken);
            }
        }
        finally
        {
            sessionState.TurnLock.Release();
        }
    }

    public async Task<TurnMetrics> ProcessTurn(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        await sessionState.TurnLock.WaitAsync(cancellationToken);
        try
        {
            sessionState.Session.DidMoveThisTurn = false;
            sessionState.Session.DidSceneRefreshThisTurn = false;

            var stopwatch = Stopwatch.StartNew();
            long? firstTokenElapsedMs = null;
            var tokenCount = 0;
            var buffer = new StringBuilder();

            await foreach (var token in StreamAndLog(input, cancellationToken))
            {
                firstTokenElapsedMs ??= stopwatch.ElapsedMilliseconds;
                tokenCount++;
                buffer.Append(token);
            }

            var totalMs = stopwatch.ElapsedMilliseconds;
            logger.LogInformation(
                "[perf] SendAsync first token after {FirstTokenMs}ms, total {TotalMs}ms",
                firstTokenElapsedMs,
                totalMs
            );

            if (sessionState.Session.DidMoveThisTurn)
            {
                await RunPostMoveCleanup(cancellationToken);
            }

            return new TurnMetrics(
                buffer.ToString(),
                firstTokenElapsedMs ?? totalMs,
                totalMs,
                tokenCount
            );
        }
        finally
        {
            sessionState.TurnLock.Release();
        }
    }

    public async IAsyncEnumerable<string> ProcessTurnStreaming(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await sessionState.TurnLock.WaitAsync(cancellationToken);
        try
        {
            sessionState.Session.DidMoveThisTurn = false;
            sessionState.Session.DidSceneRefreshThisTurn = false;

            await foreach (var token in StreamAndLog(input, cancellationToken))
            {
                yield return token;
            }

            if (sessionState.Session.DidMoveThisTurn)
            {
                await RunPostMoveCleanup(cancellationToken);
            }
        }
        finally
        {
            sessionState.TurnLock.Release();
        }
    }

    public async Task<InGameDate> AdvanceTime(
        int hours,
        CancellationToken cancellationToken = default
    )
    {
        await sessionState.TurnLock.WaitAsync(cancellationToken);
        try
        {
            GameClock.AdvanceHours(sessionState.Session, hours);
            return GameClock.GetCurrentInGameDate(sessionState.Session);
        }
        finally
        {
            sessionState.TurnLock.Release();
        }
    }

    private async IAsyncEnumerable<string> StreamAndLog(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var messages = sessionState.Messages;

        logger.LogInformation("[game] >>> {Message}", input);
        messages.Add(new ChatMessage(ChatRole.User, input));
        GameClock.AdvanceForMessage(
            sessionState.Session,
            gameClockOptions.Value.RealTimePerMessage
        );

        var gameplayOptions = optionsMonitor.Get(LlmRoleKeys.Gameplay);
        var chatOptions = new ChatOptions
        {
            Tools = tools.Cast<AITool>().ToList(),
            Temperature = gameplayOptions.Temperature,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["think"] = gameplayOptions.Think ?? false,
                ["num_ctx"] = 8192,
            },
        };

        var updates = new List<ChatResponseUpdate>();
        var thinking = new StringBuilder();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                messages,
                chatOptions,
                cancellationToken
            )
        )
        {
            updates.Add(update);
            foreach (var content in update.Contents)
            {
                if (content is TextReasoningContent reasoning)
                {
                    thinking.Append(reasoning.Text);
                }
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }

        var aggregated = updates.ToChatResponse();
        messages.AddRange(aggregated.Messages);

        if (thinking.Length > 0)
        {
            logger.LogDebug("[game] think: {Thinking}", thinking.ToString().Trim());
        }

        logger.LogInformation("[game] <<< {Response}", aggregated.Text);
    }

    private async Task SendAndDrain(string input, CancellationToken cancellationToken)
    {
        await foreach (var _ in StreamAndLog(input, cancellationToken)) { }
    }

    private async Task RunPostMoveCleanup(CancellationToken cancellationToken)
    {
        var currentTurnStart = sessionState.Messages.FindLastIndex(m => m.Role == ChatRole.User);
        await CloseLingeringConversations(cancellationToken);
        ClearPreviousTurns(currentTurnStart);
    }

    private async Task CloseLingeringConversations(CancellationToken cancellationToken)
    {
        var session = sessionState.Session;
        foreach (var npcName in session.OpenConversationCreatureIdsByName.Keys.ToArray())
        {
            if (!session.OpenConversationCreatureIdsByName.ContainsKey(npcName))
            {
                continue;
            }

            var prompt =
                $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
            await SendAndDrain(prompt, cancellationToken);

            if (session.OpenConversationCreatureIdsByName.Remove(npcName))
            {
                logger.LogWarning(
                    "[game] Failed to save conversation summary for {NpcName}",
                    npcName
                );
            }
        }
    }

    private void ClearPreviousTurns(int currentTurnStart)
    {
        var messages = sessionState.Messages;
        var systemMessage = messages[0];
        var currentTurnMessages = messages.Skip(currentTurnStart).ToList();

        messages.Clear();
        messages.Add(systemMessage);
        messages.AddRange(currentTurnMessages);
    }
}
