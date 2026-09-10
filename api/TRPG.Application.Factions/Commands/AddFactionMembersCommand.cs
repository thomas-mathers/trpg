using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Commands;

public class AddFactionMembersCommand
{
    public required IReadOnlyCollection<FactionMember> Members { get; init; }
}

internal class AddFactionMembersCommandHandler(IFactionsDbContext context)
    : ICommandHandler<AddFactionMembersCommand>
{
    public async Task Handle(
        AddFactionMembersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.FactionMembers.AddRange(command.Members);
        await context.SaveChangesAsync(cancellationToken);
    }
}
