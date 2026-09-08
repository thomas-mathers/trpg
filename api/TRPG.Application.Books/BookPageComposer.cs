using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using BookWork = TRPG.Domain.Models.BookWork;
using Secret = TRPG.Domain.Models.Secret;

namespace TRPG.Application.Books;

public record BookPageCompositionRequest(
    BookWork Work,
    int PageNumber,
    IReadOnlyList<string> PriorPages,
    Secret? Secret
);

public class BookPageComposer([FromKeyedServices(LlmRoleKeys.Gameplay)] IChatClient client)
{
    private const string SystemPrompt = """
        You write a single page of an in-world book for a fantasy RPG. Write only the page's prose,
        with no title, no page number, no heading, and no commentary about the book.

        Write three to five short paragraphs in the voice of an in-world author: a chronicler, a
        guild clerk, a travelling scholar. Be concrete and grounded. Invent local detail freely,
        but never contradict a page that came before.

        This is a real book in a lived-in world, not an encyclopedia entry. It may be opinionated,
        incomplete, or biased by its author.
        """;

    public async Task<string> Compose(
        BookPageCompositionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt), BuildUserMessage(request)],
            cancellationToken: cancellationToken
        );

        return response.Text.Trim();
    }

    private static ChatMessage BuildUserMessage(BookPageCompositionRequest request)
    {
        var work = request.Work;
        var priorPages =
            request.PriorPages.Count == 0
                ? "This is the opening page."
                : $"""
                    Earlier pages of this same book, for continuity:
                    {string.Join("\n\n", request.PriorPages)}
                    """;

        // The secret is quoted rather than described, because a page that paraphrases the
        // countersign teaches the reader nothing they can use.
        var secret =
            request.Secret == null
                ? ""
                : $"""

                    This page records {request.Secret.Subject}. Work it into the prose naturally,
                    and reproduce it exactly as: {request.Secret.Value}
                    """;

        return new ChatMessage(
            ChatRole.User,
            $"""
            Title: {work.Title}
            Subject: {work.SubjectType} named {work.SubjectName}
            Page {request.PageNumber} of {work.PageCount}

            {priorPages}{secret}
            """
        );
    }
}
