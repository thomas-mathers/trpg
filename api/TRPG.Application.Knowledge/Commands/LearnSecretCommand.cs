using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Commands;

public class LearnSecretCommand
{
    public required Guid WorldId { get; init; }
    public required Guid KnowerId { get; init; }
    public required Guid SecretId { get; init; }
}

internal class LearnSecretCommandHandler(IKnowledgeDbContext context)
    : ICommandHandler<LearnSecretCommand, bool>
{
    public async Task<bool> Handle(
        LearnSecretCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var alreadyKnown = await context.CreatureKnowledge.AnyAsync(
            knowledge =>
                knowledge.KnowerId == command.KnowerId
                && knowledge.SubjectId == command.SecretId
                && knowledge.SubjectType == KnowledgeSubjectType.Secret,
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
                SubjectId = command.SecretId,
                SubjectType = KnowledgeSubjectType.Secret,
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
