using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Commands;

public class LearnFactCommand
{
    public required Guid WorldId { get; init; }
    public required Guid KnowerId { get; init; }
    public required Guid FactId { get; init; }
}

internal class LearnFactCommandHandler(IKnowledgeDbContext context)
    : ICommandHandler<LearnFactCommand, bool>
{
    public async Task<bool> Handle(
        LearnFactCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var alreadyKnown = await context.CreatureKnowledge.AnyAsync(
            knowledge =>
                knowledge.KnowerId == command.KnowerId
                && knowledge.SubjectId == command.FactId
                && knowledge.SubjectType == KnowledgeSubjectType.Fact,
            cancellationToken
        );
        if (alreadyKnown)
        {
            return false;
        }

        context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                WorldId = command.WorldId,
                KnowerId = command.KnowerId,
                SubjectId = command.FactId,
                SubjectType = KnowledgeSubjectType.Fact,
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
