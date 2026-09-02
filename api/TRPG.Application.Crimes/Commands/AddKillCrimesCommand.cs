using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddKillCrimesCommand
{
    public required IReadOnlyCollection<KillCrime> Crimes { get; init; }
}

internal class AddKillCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<AddKillCrimesCommand>
{
    public async Task Handle(
        AddKillCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
