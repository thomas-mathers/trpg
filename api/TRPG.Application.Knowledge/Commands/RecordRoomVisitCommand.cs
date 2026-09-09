using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Commands;

public class RecordRoomVisitCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required Guid RoomLocationId { get; init; }
}

// Somewhere you have stood is somewhere you know, which is what lets an exit be marked as leading
// back rather than onward. Keyed by knower, so an NPC could hold the same knowledge of a place.
internal class RecordRoomVisitCommandHandler(IKnowledgeDbContext context)
    : ICommandHandler<RecordRoomVisitCommand>
{
    public async Task Handle(
        RecordRoomVisitCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var alreadyVisited = await context.CreatureKnowledge.AnyAsync(
            knowledge =>
                knowledge.KnowerId == command.CreatureId
                && knowledge.SubjectId == command.RoomLocationId
                && knowledge.SubjectType == KnowledgeSubjectType.Room,
            cancellationToken
        );
        if (alreadyVisited)
        {
            return;
        }

        context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                WorldId = command.WorldId,
                KnowerId = command.CreatureId,
                SubjectId = command.RoomLocationId,
                SubjectType = KnowledgeSubjectType.Room,
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
