using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class AddQuestCommand
{
    public required Quest Quest { get; init; }
    public required IReadOnlyCollection<QuestObjective> Objectives { get; init; }
}

internal class AddQuestCommandHandler(IQuestsDbContext context) : ICommandHandler<AddQuestCommand>
{
    public async Task Handle(AddQuestCommand command, CancellationToken cancellationToken = default)
    {
        context.Quests.Add(command.Quest);
        context.QuestObjectives.AddRange(command.Objectives);
        await context.SaveChangesAsync(cancellationToken);
    }
}
