using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class AddCreaturesCommand
{
    public required IReadOnlyCollection<Creature> Creatures { get; init; }
}

internal class AddCreaturesCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<AddCreaturesCommand>
{
    public async Task Handle(
        AddCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Creatures.AddRange(command.Creatures);
        await context.SaveChangesAsync(cancellationToken);
    }
}
