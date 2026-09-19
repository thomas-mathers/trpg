using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;

namespace TRPG.Application.WorldGeneration.Generators;

internal class QuestChainStorySchema
{
    public string Summary { get; init; } = "";
    public List<QuestChainStoryChapterSchema> Chapters { get; init; } = [];
}

internal class QuestChainStoryChapterSchema
{
    public string Description { get; init; } = "";
    public List<string> Turns { get; init; } = [];
}

public record QuestChainStory(string Summary, IReadOnlyList<QuestChainStoryChapter> Chapters);

public record QuestChainStoryChapter(string Description, IReadOnlyList<string> Turns);

public class QuestChainStoryGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainStoryGenerator> logger
)
{
    public async Task<QuestChainStory> Generate(
        string premise,
        int chapterCount,
        CancellationToken cancellationToken = default
    )
    {
        var schema = await client.GetValidatedJson<QuestChainStorySchema>(
            logger,
            $$"""
            Write a compact complete RPG quest story in exactly {{chapterCount}} chapters. Focus only
            on narrative: conflict, discoveries, reversals, competing approaches, consequences, and
            resolution.

            Vary what a turn asks a character to do: investigate or clear out a place, defeat a
            threat, free someone captive, retrieve or carry something to someone else, or pry a
            reluctant truth out of someone. Avoid a story that leans on investigation and combat
            alone.

            Do not select game entities, objectives, rewards, graph types, node counts, or quest
            names.

            Return Summary (under 80 words) and Chapters. Every chapter needs a Description (under
            45 words) and two to four concise Turns. Include at least one revelation and a
            satisfying final resolution. Place the story's most meaningful choice or competing
            approach in an earlier or middle chapter, not folded into the final chapter alongside
            the resolution — let that choice actually shape how the later chapters unfold, rather
            than saving all of the story's complexity for the ending. Earlier chapters must leave
            the core conflict unresolved. Respond with raw JSON only.
            """,
            $"Quest premise: {premise}\nChapter count: {chapterCount}",
            story => Validate(story, chapterCount),
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 4096 }
        );
        return new QuestChainStory(
            schema.Summary,
            schema
                .Chapters.Select(chapter => new QuestChainStoryChapter(
                    chapter.Description,
                    chapter.Turns.ToArray()
                ))
                .ToArray()
        );
    }

    private static string? Validate(QuestChainStorySchema story, int chapterCount)
    {
        if (string.IsNullOrWhiteSpace(story.Summary) || story.Chapters.Count != chapterCount)
        {
            return $"Write a summary and exactly {chapterCount} chapters.";
        }

        return story.Chapters.Any(chapter =>
            string.IsNullOrWhiteSpace(chapter.Description)
            || chapter.Turns.Count is < 2 or > 4
            || chapter.Turns.Any(string.IsNullOrWhiteSpace)
        )
            ? "Every chapter needs a description and two to four nonblank turns."
            : null;
    }
}
