using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Knowledge.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Books.Commands;

public class ReadBookPageCommand
{
    public required Guid WorldId { get; init; }
    public required Guid ReaderId { get; init; }
    public required Guid WorkId { get; init; }
    public required int PageNumber { get; init; }
}

public record ReadBookPageResult(
    string Title,
    int PageNumber,
    int PageCount,
    string Text,
    bool RevealedSecret
);

internal class ReadBookPageCommandHandler(
    IBooksDbContext context,
    BookPageComposer composer,
    ICommandHandler<LearnSecretCommand, bool> learnSecret,
    ILogger<ReadBookPageCommandHandler> logger
) : ICommandHandler<ReadBookPageCommand, ReadBookPageResult>
{
    public async Task<ReadBookPageResult> Handle(
        ReadBookPageCommand command,
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

        var text = await ResolvePageText(work, command.PageNumber, cancellationToken);

        var revealedSecret = await RevealSecret(work, command, cancellationToken);

        return new ReadBookPageResult(
            work.Title,
            command.PageNumber,
            work.PageCount,
            text,
            revealedSecret
        );
    }

    // Written once per work, so every copy on every shelf reads the same words.
    private async Task<string> ResolvePageText(
        BookWork work,
        int pageNumber,
        CancellationToken cancellationToken
    )
    {
        var stored = await context
            .BookPages.AsNoTracking()
            .FirstOrDefaultAsync(
                page => page.WorkId == work.Id && page.PageNumber == pageNumber,
                cancellationToken
            );
        if (stored != null)
        {
            return stored.Text;
        }

        var priorPages = await context
            .BookPages.AsNoTracking()
            .Where(page => page.WorkId == work.Id && page.PageNumber < pageNumber)
            .OrderBy(page => page.PageNumber)
            .Select(page => page.Text)
            .ToArrayAsync(cancellationToken);

        var secret = await ResolveSecretForPage(work, pageNumber, cancellationToken);

        logger.LogInformation(
            "[book] composing page {PageNumber}/{PageCount} of {Title}",
            pageNumber,
            work.PageCount,
            work.Title
        );

        var composed = await composer.Compose(
            new BookPageCompositionRequest(work, pageNumber, priorPages, secret),
            cancellationToken
        );

        context.BookPages.Add(
            new BookPage
            {
                WorldId = work.WorldId,
                WorkId = work.Id,
                PageNumber = pageNumber,
                Text = composed,
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        return composed;
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

    // Skimming the first page of a ledger does not teach you what is written on the sixth.
    private async Task<bool> RevealSecret(
        BookWork work,
        ReadBookPageCommand command,
        CancellationToken cancellationToken
    )
    {
        if (work.SecretId is not { } secretId || work.SecretPageNumber != command.PageNumber)
        {
            return false;
        }

        var learned = await learnSecret.Handle(
            new LearnSecretCommand
            {
                WorldId = command.WorldId,
                KnowerId = command.ReaderId,
                SecretId = secretId,
            },
            cancellationToken
        );
        if (learned)
        {
            logger.LogInformation(
                "[book] {ReaderId} learned secret {SecretId} from {Title}",
                command.ReaderId,
                secretId,
                work.Title
            );
        }

        return learned;
    }
}
