using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CreateEncounterGroupsCommand
{
    public required IReadOnlyCollection<EncounterGroup> Groups { get; init; }
    public required IReadOnlyCollection<EncounterGroupMember> Members { get; init; }
}

internal class CreateEncounterGroupsCommandHandler(IEncountersDbContext context)
    : ICommandHandler<CreateEncounterGroupsCommand>
{
    public async Task Handle(
        CreateEncounterGroupsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.EncounterGroups.AddRange(command.Groups);
        context.EncounterGroupMembers.AddRange(command.Members);
        await context.SaveChangesAsync(cancellationToken);
    }
}
