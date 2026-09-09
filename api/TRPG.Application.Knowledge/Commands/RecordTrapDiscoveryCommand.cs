using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Commands;

public class RecordTrapDiscoveryCommand
{
    public required Guid WorldId { get; init; }
    public required Guid KnowerId { get; init; }
    public required Guid TriggerId { get; init; }
}

internal class RecordTrapDiscoveryCommandHandler(IKnowledgeDbContext context)
    : ICommandHandler<RecordTrapDiscoveryCommand>
{
    public async Task Handle(
        RecordTrapDiscoveryCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var alreadyKnown = await context.CreatureKnowledge.AnyAsync(
            knowledge =>
                knowledge.KnowerId == command.KnowerId
                && knowledge.SubjectId == command.TriggerId
                && knowledge.SubjectType == KnowledgeSubjectType.Trap,
            cancellationToken
        );
        if (alreadyKnown)
        {
            return;
        }

        context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                WorldId = command.WorldId,
                KnowerId = command.KnowerId,
                SubjectId = command.TriggerId,
                SubjectType = KnowledgeSubjectType.Trap,
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
