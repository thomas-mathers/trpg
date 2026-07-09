using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class UpdateCreatureCommand
{
    public required Creature Creature { get; init; }
}

internal class UpdateCreatureCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        UpdateCreatureCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Creatures.Update(command.Creature);
        await context.SaveChangesAsync(cancellationToken);
    }
}
