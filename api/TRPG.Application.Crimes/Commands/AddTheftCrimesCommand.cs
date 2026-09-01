using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddTheftCrimesCommand
{
    public required IReadOnlyCollection<TheftCrime> Crimes { get; init; }
}

internal class AddTheftCrimesCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddTheftCrimesCommand>
{
    public async Task Handle(
        AddTheftCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
