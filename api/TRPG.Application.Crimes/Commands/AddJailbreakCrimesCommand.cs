using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddJailbreakCrimesCommand
{
    public required IReadOnlyCollection<JailbreakCrime> Crimes { get; init; }
}

internal class AddJailbreakCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<AddJailbreakCrimesCommand>
{
    public async Task Handle(
        AddJailbreakCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Crimes.AddRange(command.Crimes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
