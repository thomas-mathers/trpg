using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Llm;
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

        Write two or three short paragraphs in the voice of an in-world author: a chronicler, a
        guild clerk, a travelling scholar. A page is a page, not a chapter. Be concrete and
        grounded. Invent local detail freely, but never contradict a page that came before.

        This is a real book in a lived-in world, not an encyclopedia entry. It may be opinionated,
        incomplete, or biased by its author.
        """;

    public async Task<string> Compose(
        BookPageCompositionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var response = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                BuildBookMessage(request),
                BuildPageMessage(request),
            ],
            cancellationToken: cancellationToken
        );

        return response.Text.Trim();
    }

    // Everything a later page will also send, in the order it will send it, so page six opens with
    // exactly what page five opened with and the provider can reuse it.
    private static ChatMessage BuildBookMessage(BookPageCompositionRequest request)
    {
        var work = request.Work;
        var priorPages =
            request.PriorPages.Count == 0
                ? "No pages have been written yet."
                : $"""
                    The pages so far:
                    {string.Join("\n\n", request.PriorPages)}
                    """;

        return new ChatMessage(
            ChatRole.User,
            $"""
            Title: {work.Title}
            Subject: {work.SubjectType} named {work.SubjectName}
            Length: {work.PageCount} pages

            {priorPages}
            """
        )
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [LlmCacheHints.PrefixEnd] = true,
            },
        };
    }

    // Kept out of the block above because it changes every page, and a secret must never sit inside
    // a prefix that a later page will reuse.
    private static ChatMessage BuildPageMessage(BookPageCompositionRequest request)
    {
        var secret =
            request.Secret == null
                ? ""
                : $"""


                    This page records {request.Secret.Subject}. Work it into the prose naturally,
                    and reproduce it exactly as: {request.Secret.Value}
                    """;

        return new ChatMessage(
            ChatRole.User,
            $"Write page {request.PageNumber} of {request.Work.PageCount}.{secret}"
        );
    }
}
