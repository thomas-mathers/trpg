using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Knowledge.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Books.EventHandlers;

internal sealed class ItemGivenToCreatureEventHandler(
    IBooksDbContext context,
    ICommandHandler<LearnFactCommand, bool> learnFact
) : IDomainEventConsumer<ItemGivenToCreatureEvent>
{
    public async Task Handle(
        ItemGivenToCreatureEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var book = await context
            .Items.OfType<Book>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == domainEvent.ItemId, cancellationToken);
        if (book is null)
        {
            return;
        }

        var work = await context
            .BookWorks.AsNoTracking()
            .FirstOrDefaultAsync(work => work.Id == book.WorkId, cancellationToken);
        if (work?.FactId is not { } factId)
        {
            return;
        }

        await learnFact.Handle(
            new LearnFactCommand
            {
                WorldId = domainEvent.WorldId,
                KnowerId = domainEvent.RecipientId,
                FactId = factId,
            },
            cancellationToken
        );
    }
}
