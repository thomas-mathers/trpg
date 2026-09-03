using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddBreakingAndEnteringCrimesCommand
{
    public required IReadOnlyCollection<BreakingAndEnteringCrime> Crimes { get; init; }
}

internal class AddBreakingAndEnteringCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<AddBreakingAndEnteringCrimesCommand>
{
    public async Task Handle(
        AddBreakingAndEnteringCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
