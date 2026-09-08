using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Books.Queries;

public class GetBookByItemIdQuery
{
    public required Guid ItemId { get; init; }
}

public record BookIdentity(Guid ItemId, Guid WorkId, Guid WorldId, string Title, int PageCount);

internal class GetBookByItemIdQueryHandler(IBooksDbContext context)
    : IQueryHandler<GetBookByItemIdQuery, BookIdentity?>
{
    public async Task<BookIdentity?> Handle(
        GetBookByItemIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .OfType<Book>()
            .Where(book => book.Id == query.ItemId)
            .Join(
                context.BookWorks.AsNoTracking(),
                book => book.WorkId,
                work => work.Id,
                (book, work) =>
                    new BookIdentity(book.Id, work.Id, work.WorldId, work.Title, work.PageCount)
            )
            .FirstOrDefaultAsync(cancellationToken);
}
