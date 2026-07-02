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
        For move's destinationName: when outdoors, use the building's Name from NearbyBuildings. When indoors, use the exit's DestinationRoomName from Room.Exits.
        Be concise but evocative. Weave the descriptions returned by tools into your narration — they are the ground truth of what this place looks, feels, and smells like. Don't ignore them and invent your own. Add atmosphere and immediacy, but don't contradict or pad over what the tools tell you. Keep responses to a few short paragraphs.
        NearbyBuildings is only populated when the player is outdoors (Building and Room are null) — when the player is indoors, NearbyBuildings is empty because you cannot see outside from here, not because the city has no buildings. If asked what's nearby while indoors, say the player would need to step outside to see, or call move to "Outside" and then look.
        Never break the fourth wall. Don't mention tools, mechanics, IDs, or anything that doesn't exist in the world. If you're uncertain about something, let the world be uncertain — a rumour unconfirmed, a name half-remembered.
        Narrate in second person, present tense: "You step into..." not "The player enters...". End each response with a natural sense of what's around to do next — woven into the prose, not listed.
        Tone: Gritty low fantasy. Factions scheme, roads are dangerous, and most people are just trying to survive. Magic exists but is rare and unsettling. Humour is welcome; heroism is earned.
        """;

    public async Task Run(GameSession session, CancellationToken cancellationToken) {
        var tools = toolFactory.Create(session);
        var chat = new Chat(ollamaClient, SystemPrompt) { Think = true, Options = new RequestOptions { NumCtx = 8192 } };
        chat.OnThink += (_, token) => logger.LogDebug("[game] think: {Token}", token);

        Console.Clear();
        Console.WriteLine("Welcome to the TRPG Game Master!");
        Console.WriteLine("Type 'exit' to quit.");

        var openingPrompt = await BuildOpeningPrompt(session, sceneService, worldService, cancellationToken);
        await SendAndLog(chat, openingPrompt, tools, cancellationToken);

        while (true) {
            Console.Write("\n> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) {
                break;
            }

            await SendAndLog(chat, input, tools, cancellationToken);
        }
    }

    private async Task SendAndLog(Chat chat, string input, IReadOnlyList<Tool> tools, CancellationToken cancellationToken) {
        logger.LogInformation("[game] >>> {Message}", input);

        var buffer = new StringBuilder();
        await foreach (var token in chat.SendAsync(input, tools, cancellationToken: cancellationToken)) {
            Console.Write(token);
            buffer.Append(token);
        }

        logger.LogInformation("[game] <<< {Response}", buffer.ToString());
    }

    internal static async Task<string> BuildOpeningPrompt(GameSession session, SceneService sceneService,
        WorldService worldService, CancellationToken cancellationToken = default) {
        var world = await worldService.GetWorld(session.WorldId, cancellationToken);
        var scene = await sceneService.GetScene(session.WorldId, session.PlayerId, cancellationToken);

        var worldInfo = new { world!.Name, world.Description };
        return $"""
                This is the start of the session. Narrate the opening scene using ONLY the data below — do not call world or look for this turn.
                World: {JsonSerializer.Serialize(worldInfo, ToolJsonOptions.Options)}
                Scene: {JsonSerializer.Serialize(scene, ToolJsonOptions.Options)}
                """;
    }
}
