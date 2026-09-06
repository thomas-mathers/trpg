using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddAssaultCrimesCommand
{
    public required IReadOnlyCollection<AssaultCrime> Crimes { get; init; }
}

internal class AddAssaultCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<AddAssaultCrimesCommand>
{
    public async Task Handle(
        AddAssaultCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
