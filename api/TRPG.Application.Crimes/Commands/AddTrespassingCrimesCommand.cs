using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddTrespassingCrimesCommand
{
    public required IReadOnlyCollection<TrespassingCrime> Crimes { get; init; }
}

internal class AddTrespassingCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<AddTrespassingCrimesCommand>
{
    public async Task Handle(
        AddTrespassingCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
