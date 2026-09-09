using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Books.Commands;
using TRPG.Application.Books.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Books.Responses;

namespace TRPG.Books.Endpoints;

internal static class BookEndpoints
{
    public static void MapBookEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/players/{playerId:guid}/books/{itemId:guid}/pages/{pageNumber:int}",
                ReadBookPage
            )
            .WithName("ReadBookPage")
            .Produces<BookPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost("/books/{itemId:guid}/pages/{pageNumber:int}/prefetch", PrefetchBookPage)
            .WithName("PrefetchBookPage")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // Composes a page ahead of the reader without teaching them anything it records, so turning to
    // the next page is instant but skipping to it in the client is not a way to learn a secret.
    //
    // The composition itself runs on CancellationToken.None: a warm-up outlives the request that
    // started it, so the reader turning pages again (or navigating away) must not cancel prose
    // that is still being written for a page they have not reached yet.
    private static async Task<NoContent> PrefetchBookPage(
        Guid itemId,
        int pageNumber,
        [FromServices] IQueryHandler<GetBookByItemIdQuery, BookIdentity?> getBookByItemId,
        [FromServices] ICommandHandler<EnsureBookPageCommand, string> ensureBookPage,
        CancellationToken cancellationToken
    )
    {
        var book = await getBookByItemId.Handle(
            new GetBookByItemIdQuery { ItemId = itemId },
            cancellationToken
        );
        if (book == null)
        {
            throw new EntityNotFoundException("Book", itemId);
        }

        await ensureBookPage.Handle(
            new EnsureBookPageCommand { WorkId = book.WorkId, PageNumber = pageNumber },
            CancellationToken.None
        );

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BookPageResponse>> ReadBookPage(
        Guid playerId,
        Guid itemId,
        int pageNumber,
        [FromServices] IQueryHandler<GetBookByItemIdQuery, BookIdentity?> getBookByItemId,
        [FromServices] ICommandHandler<ReadBookPageCommand, ReadBookPageResult> readBookPage,
        CancellationToken cancellationToken
    )
    {
        var book = await getBookByItemId.Handle(
            new GetBookByItemIdQuery { ItemId = itemId },
            cancellationToken
        );
        if (book == null)
        {
            throw new EntityNotFoundException("Book", itemId);
        }

        var result = await readBookPage.Handle(
            new ReadBookPageCommand
            {
                WorldId = book.WorldId,
                ReaderId = playerId,
                WorkId = book.WorkId,
                PageNumber = pageNumber,
            },
            cancellationToken
        );

        return TypedResults.Ok(
            new BookPageResponse(
                result.Title,
                result.PageNumber,
                result.PageCount,
                result.Text,
                result.RevealedSecret
            )
        );
    }
}
