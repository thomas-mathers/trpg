using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using TRPG.Services;
using TRPG.Tools;

namespace TRPG;

internal class Game(
    OllamaApiClient ollamaClient,
    ToolFactory toolFactory,
    SceneService sceneService,
    WorldService worldService,
    ILogger<Game> logger) {
    internal const string SystemPrompt =
        """
        You are the game master of a living fantasy world. Your role is to narrate the player's experience and interpret their actions to advance the story.
        Always read before you narrate. Before describing any location, NPC, item, or building, use the available tools to fetch its current state. Never invent names, people, or facts — everything that exists is in the game state. If a tool returns nothing, nothing is there.
        Use proper nouns exactly as they appear in tool results — reproduce building, people, region, and item names character-for-character. Everything that exists in this world exists in the tool results; there is no other source of truth.
        Resolve actions through tools. When the player attempts something — moving, picking up an item, speaking to someone — call the relevant tool to carry it out. Narrate the outcome based on what the tool returns. If an action fails, explain why in-world without breaking character.
        When the player wants to go somewhere: call look to get the current location data, identify the destination, then immediately call move with that exact destinationName copied from the result. Do not ask for clarification — resolve navigation autonomously using the tool results. If the exact name you want isn't in the most recent look or move result, call look again rather than guessing a name.
        Any statement that the player is leaving or departing counts as a move request, however it's phrased — including indirect intent or dialogue said to an NPC, like "I'll take my leave," "I should get going," or "I'm out of here." Call move in the same turn to actually carry it out. Never narrate the player as having left, or as being in the middle of leaving, without having called move first — the narration must match what the tools confirm actually happened.
        For move's destinationName: when outdoors, use the building's Name from NearbyBuildings. When indoors, use the exit's DestinationRoomName from Room.Exits.
        Be concise but evocative. Weave the descriptions returned by tools into your narration — they are the ground truth of what this place looks, feels, and smells like. Don't ignore them and invent your own. Add atmosphere and immediacy, but don't contradict or pad over what the tools tell you. Keep responses to a few short paragraphs.
        NearbyBuildings is only populated when the player is outdoors (Building and Room are null) — when the player is indoors, NearbyBuildings is empty because you cannot see outside from here, not because the city has no buildings. If asked what's nearby while indoors, say the player would need to step outside to see, or call move to "Outside" and then look.
        Scene results include CurrentDate (Year, MonthName, Day, WeekdayName, Hour — a 24-hour value where 0 is midnight). Use Hour to judge whether it's night or day and let that shape the scene: streets empty out, shops close, torches and hearths are lit, people sleep. Use the rest for atmosphere (season, time-worn detail) or when the player asks what day it is, but don't recite the date mechanically every turn.
        Never break the fourth wall. Don't mention tools, mechanics, IDs, or anything that doesn't exist in the world. If you're uncertain about something, let the world be uncertain — a rumour unconfirmed, a name half-remembered.
        The Player object in tool results identifies the player character for your own reference — it is not knowledge available to NPCs. An NPC only knows the player's name if the player has introduced themselves in conversation this session, or if the name is already established in-world (e.g. a famous figure). Until then, NPCs address the player as a stranger — "traveler," "friend," a physical description — never by name.
        When the player starts talking to an NPC, call start_conversation for that NPC before narrating their reaction — use the returned summary to inform how they act, and let an empty summary mean this is a first meeting. When the topic winds down or the player leaves, call end_conversation for them with a concise summary, even if they didn't say a formal goodbye.
        Narrate in second person, present tense: "You step into..." not "The player enters...". End each response with a natural sense of what's around to do next — woven into the prose, not listed.
        Tone: Gritty low fantasy. Factions scheme, roads are dangerous, and most people are just trying to survive. Magic exists but is rare and unsettling. Humour is welcome; heroism is earned.
        """;

    public async Task Run(GameSession session, CancellationToken cancellationToken) {
        var chat = new Chat(ollamaClient, SystemPrompt) {
            Think = true, 
            Options = new RequestOptions { NumCtx = 8192 }
        };

        var tools = toolFactory.Create(session);

        Console.Clear();
        Console.WriteLine("Welcome to the TRPG Game Master!");
        Console.WriteLine("Type 'exit' to quit.");

        var openingPrompt = await BuildOpeningPrompt(worldService, sceneService, session, cancellationToken);
        await SendAndLog(chat, tools, openingPrompt, cancellationToken);

        while (true) {
            Console.Write("\n> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) {
                var world = await worldService.GetWorld(session.WorldId, cancellationToken);
                world!.Playtime = GameClock.GetTotalPlaytime(session);
                await worldService.Update(world, cancellationToken);
                break;
            }

            session.DidMoveThisTurn = false;
            await SendAndLog(chat, tools, input, cancellationToken);

            if (session.DidMoveThisTurn) {
                var currentTurnStart = chat.Messages.FindLastIndex(m => m.Role == ChatRole.User);
                await CloseLingeringConversations(chat, tools, session, cancellationToken);
                ClearPreviousTurns(chat, currentTurnStart);
            }
        }
    }

    private static void ClearPreviousTurns(Chat chat, int currentTurnStart) {
        var systemMessage = chat.Messages[0];
        var currentTurnMessages = chat.Messages.Skip(currentTurnStart).ToList();

        chat.Messages.Clear();
        chat.Messages.Add(systemMessage);
        chat.Messages.AddRange(currentTurnMessages);
    }

    private async Task CloseLingeringConversations(
        Chat chat, 
        IReadOnlyList<Tool> tools, 
        GameSession session, 
        CancellationToken cancellationToken
    ) {
        foreach (var npcName in session.ActiveConversationNpcs.Keys.ToArray()) {
            if (!session.ActiveConversationNpcs.ContainsKey(npcName)) {
                continue;
            }

            var prompt = $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
            await SendAndLog(chat, tools, prompt, cancellationToken);

            if (session.ActiveConversationNpcs.Remove(npcName)) {
                logger.LogWarning("[game] Failed to save conversation summary for {NpcName}", npcName);
            }
        }
    }

    private async Task SendAndLog(
        Chat chat,
        IReadOnlyList<Tool> tools,
        string input,
        CancellationToken cancellationToken
    ) {
        logger.LogInformation("[game] >>> {Message}", input);

        var thinking = new StringBuilder();
        void AppendThinking(object? _, string token) => thinking.Append(token);

        chat.OnThink += AppendThinking;
        var buffer = new StringBuilder();
        try {
            await foreach (var token in chat.SendAsync(input, tools, cancellationToken: cancellationToken)) {
                Console.Write(token);
                buffer.Append(token);
            }
        } finally {
            chat.OnThink -= AppendThinking;
        }

        if (thinking.Length > 0) {
            logger.LogDebug("[game] think: {Thinking}", thinking.ToString().Trim());
        }
        
        logger.LogInformation("[game] <<< {Response}", buffer.ToString());
    }

    internal static async Task<string> BuildOpeningPrompt(WorldService worldService,
        SceneService sceneService,
        GameSession session,
        CancellationToken cancellationToken = default) {
        var world = await worldService.GetWorld(session.WorldId, cancellationToken);
        var currentDate = GameClock.GetCurrentInGameDate(session);
        var query = new SceneQuery(session.WorldId, session.PlayerId, currentDate);
        var scene = await sceneService.GetScene(query, cancellationToken);

        var worldInfo = new { world!.Name, world.Description };
        return $"""
                This is the start of the session. Narrate the opening scene using ONLY the data below — do not call world or look for this turn.
                World: {JsonSerializer.Serialize(worldInfo, ToolJsonOptions.Options)}
                Scene: {JsonSerializer.Serialize(scene, ToolJsonOptions.Options)}
                """;
    }
}
