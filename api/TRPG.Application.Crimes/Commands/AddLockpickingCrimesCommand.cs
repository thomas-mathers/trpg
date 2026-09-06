using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddLockpickingCrimesCommand
{
    public required IReadOnlyCollection<LockpickingCrime> Crimes { get; init; }
}

internal class AddLockpickingCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<AddLockpickingCrimesCommand>
{
    public async Task Handle(
        AddLockpickingCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
