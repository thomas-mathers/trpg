using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;

namespace TRPG.Application.WorldGeneration.Generators;

// Three explicit fields rather than one free-text StoryBible string: the model's natural
// instinct, confirmed live, is to structure this exact request (antagonist, secret, resolution)
// as three separate values even when asked for one string — asking for what it already wants to
// produce avoids a guaranteed first-attempt validation failure on every single call.
internal class QuestChainStoryBibleSchema
{
    public string Antagonist { get; init; } = "";
    public string CentralSecret { get; init; } = "";
    public string FinalResolution { get; init; } = "";
}

// The one remaining piece of cross-slice narrative continuity: since structure is now composed
// deterministically rather than authored by an LLM reading the whole story, nothing else ties a
// throughline (the specific antagonist, secret, and intended resolution) together across the
// several independent content-generation calls a longer chain gets split into. This step gives
// every slice the same anchor without ever influencing node budget or block choice.
public class QuestChainStoryBibleGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainStoryBibleGenerator> logger
)
{
    public async Task<string> Generate(
        string premise,
        string? antagonistFactionName,
        CancellationToken cancellationToken = default
    )
    {
        var schema = await client.GetValidatedJson<QuestChainStoryBibleSchema>(
            logger,
            """
            Expand this RPG quest premise into a short story bible: the specific antagonist or
            threat, the central secret the story revolves around, and the intended final
            resolution. This is a throughline anchor for other writers to stay consistent with, not
            a plot outline — do not invent chapters, turns, game entities, objectives, or quest
            names.

            Respond with raw JSON only. Keep each field under 30 words.
            """,
            $"Quest premise: {premise}\nThe antagonist is already cast as: {antagonistFactionName ?? "an uncategorized local threat"}. Use that exact antagonist; do not invent another.",
            Validate,
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 512 }
        );
        return $"Antagonist: {schema.Antagonist} Central secret: {schema.CentralSecret} Intended resolution: {schema.FinalResolution}";
    }

    private static string? Validate(QuestChainStoryBibleSchema schema) =>
        string.IsNullOrWhiteSpace(schema.Antagonist)
        || string.IsNullOrWhiteSpace(schema.CentralSecret)
        || string.IsNullOrWhiteSpace(schema.FinalResolution)
            ? "Antagonist, CentralSecret, and FinalResolution must all be nonblank."
            : null;
}
