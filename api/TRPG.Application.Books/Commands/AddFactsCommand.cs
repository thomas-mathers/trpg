using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Books.Commands;

public class AddFactsCommand
{
    public required IReadOnlyCollection<Fact> Facts { get; init; }
}

internal class AddFactsCommandHandler(IBooksDbContext context) : ICommandHandler<AddFactsCommand>
{
    public async Task Handle(AddFactsCommand command, CancellationToken cancellationToken = default)
    {
        context.Facts.AddRange(command.Facts);
        await context.SaveChangesAsync(cancellationToken);
    }
}
