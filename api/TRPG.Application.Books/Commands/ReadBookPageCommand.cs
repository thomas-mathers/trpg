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
    bool RevealedFact
);

internal class ReadBookPageCommandHandler(
    IBooksDbContext context,
    ICommandHandler<EnsureBookPageCommand, string> ensureBookPage,
    ICommandHandler<LearnFactCommand, bool> learnFact,
    ILogger<ReadBookPageCommandHandler> logger
) : ICommandHandler<ReadBookPageCommand, ReadBookPageResult>
{
    public async Task<ReadBookPageResult> Handle(
        ReadBookPageCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var work = await context
            .BookWorks.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == command.WorkId, cancellationToken);
        if (work == null)
        {
            throw new EntityNotFoundException("Book work", command.WorkId);
        }

        var text = await ensureBookPage.Handle(
            new EnsureBookPageCommand { WorkId = command.WorkId, PageNumber = command.PageNumber },
            cancellationToken
        );

        var revealedFact = await RevealFact(work, command, cancellationToken);

        return new ReadBookPageResult(
            work.Title,
            command.PageNumber,
            work.PageCount,
            text,
            revealedFact
        );
    }

    // Skimming the first page of a ledger does not teach you what is written on the sixth.
    private async Task<bool> RevealFact(
        BookWork work,
        ReadBookPageCommand command,
        CancellationToken cancellationToken
    )
    {
        if (work.FactId is not { } factId || work.FactPageNumber != command.PageNumber)
        {
            return false;
        }

        var learned = await learnFact.Handle(
            new LearnFactCommand
            {
                WorldId = command.WorldId,
                KnowerId = command.ReaderId,
                FactId = factId,
            },
            cancellationToken
        );
        if (learned)
        {
            logger.LogInformation(
                "[book] {ReaderId} learned fact {FactId} from {Title}",
                command.ReaderId,
                factId,
                work.Title
            );
        }

        return learned;
    }
}
