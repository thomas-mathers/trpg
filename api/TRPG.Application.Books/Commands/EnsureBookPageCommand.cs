using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Books.Commands;

public class EnsureBookPageCommand
{
    public required Guid WorkId { get; init; }
    public required int PageNumber { get; init; }
}

// Writing a page and learning from it are separate: a page can be composed ahead of the reader, but
// only reading it teaches what it records.
internal class EnsureBookPageCommandHandler(
    IBooksDbContext context,
    BookPageComposer composer,
    ILogger<EnsureBookPageCommandHandler> logger
) : ICommandHandler<EnsureBookPageCommand, string>
{
    public async Task<string> Handle(
        EnsureBookPageCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var work = await context.BookWorks.FirstOrDefaultAsync(
            w => w.Id == command.WorkId,
            cancellationToken
        );
        if (work == null)
        {
            throw new EntityNotFoundException("Book work", command.WorkId);
        }

        if (command.PageNumber < 1 || command.PageNumber > work.PageCount)
        {
            throw new InvalidOperationException(
                $"{work.Title} has {work.PageCount} pages; page {command.PageNumber} does not exist."
            );
        }

        var stored = await context
            .BookPages.AsNoTracking()
            .FirstOrDefaultAsync(
                page => page.WorkId == work.Id && page.PageNumber == command.PageNumber,
                cancellationToken
            );
        if (stored != null)
        {
            return stored.Text;
        }

        var priorPages = await context
            .BookPages.AsNoTracking()
            .Where(page => page.WorkId == work.Id && page.PageNumber < command.PageNumber)
            .OrderBy(page => page.PageNumber)
            .Select(page => page.Text)
            .ToArrayAsync(cancellationToken);

        var secret = await ResolveSecretForPage(work, command.PageNumber, cancellationToken);

        logger.LogInformation(
            "[book] composing page {PageNumber}/{PageCount} of {Title}",
            command.PageNumber,
            work.PageCount,
            work.Title
        );

        var composed = await composer.Compose(
            new BookPageCompositionRequest(work, command.PageNumber, priorPages, secret),
            cancellationToken
        );

        // Written unconditionally rather than checked first, because turning a page can overtake
        // the warm-up composing that same page. Both produce valid prose, so whichever lands first
        // is the page and the other is dropped.
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO book_pages (id, world_id, work_id, page_number, text)
            VALUES ({Guid.NewGuid()}, {work.WorldId}, {work.Id}, {command.PageNumber}, {composed})
            ON CONFLICT (work_id, page_number) DO NOTHING
            """,
            cancellationToken
        );
        if (inserted == 1)
        {
            return composed;
        }

        var winner =
            await context
                .BookPages.AsNoTracking()
                .FirstOrDefaultAsync(
                    page => page.WorkId == work.Id && page.PageNumber == command.PageNumber,
                    cancellationToken
                )
            ?? throw new InvalidOperationException(
                $"Page {command.PageNumber} of {work.Title} was neither written nor found."
            );
        logger.LogDebug(
            "[book] page {PageNumber} was composed elsewhere first; keeping that copy",
            command.PageNumber
        );
        return winner.Text;
    }

    private async Task<Secret?> ResolveSecretForPage(
        BookWork work,
        int pageNumber,
        CancellationToken cancellationToken
    )
    {
        if (work.SecretId is not { } secretId || work.SecretPageNumber != pageNumber)
        {
            return null;
        }

        return await context
            .Secrets.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == secretId, cancellationToken);
    }
}
