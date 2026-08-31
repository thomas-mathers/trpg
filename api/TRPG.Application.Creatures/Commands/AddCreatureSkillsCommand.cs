using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class AddCreatureSkillsCommand
{
    public required IReadOnlyCollection<CreatureSkill> Skills { get; init; }
}

internal class AddCreatureSkillsCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddCreatureSkillsCommand>
{
    public async Task Handle(
        AddCreatureSkillsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.CreatureSkills.AddRange(command.Skills);
        await context.SaveChangesAsync(cancellationToken);
    }
}
