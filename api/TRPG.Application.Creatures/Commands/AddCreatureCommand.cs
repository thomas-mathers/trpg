using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class AddCreatureCommand
{
    public required Creature Creature { get; init; }
}

internal class AddCreatureCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddCreatureCommand>
{
    public async Task Handle(
        AddCreatureCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Creatures.Add(command.Creature);
        await context.SaveChangesAsync(cancellationToken);
    }
}
