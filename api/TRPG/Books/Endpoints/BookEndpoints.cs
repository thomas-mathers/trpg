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
