using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

public class AddCreatureCommand
{
    public required Creature Creature { get; init; }
}

public class AddCreatureCommandHandler(TrpgDbContext context)
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
