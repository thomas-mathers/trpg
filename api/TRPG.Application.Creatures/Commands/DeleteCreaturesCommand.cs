using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class DeleteCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class DeleteCreaturesCommandHandler(TrpgDbContext context)
    : ICommandHandler<DeleteCreaturesCommand>
{
    public async Task Handle(
        DeleteCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ids = command.CreatureIds;
        if (ids.Count == 0)
        {
            return;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await DeleteDirectReferences(ids, cancellationToken);
        await DeleteRelationships(ids, cancellationToken);
        await DeleteKnowledge(ids, cancellationToken);
        await ClearOccupancy(ids, cancellationToken);
        await context
            .Creatures.Where(c => ids.Contains(c.Id))
            .ExecuteDeleteAsync(cancellationToken);

        transaction.Complete();
    }

    private async Task DeleteDirectReferences(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        await context
            .CreatureSkills.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureWeaponProficiencies.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureQuestObjectives.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureQuests.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Items.Where(x =>
                x.Ownership.OwnerType == OwnerType.Creature && ids.Contains(x.Ownership.OwnerId)
            )
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureJobs.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .FactionMembers.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        var conversationHistoryIds = await context
            .NpcConversationHistories.Where(history =>
                ids.Contains(history.CreatureId) || ids.Contains(history.NpcId)
            )
            .Select(history => history.Id)
            .ToArrayAsync(cancellationToken);

        await context
            .NpcConversations.Where(conversation =>
                conversationHistoryIds
                    .AsEnumerable()
                    .Contains(conversation.NpcConversationHistoryId)
            )
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .NpcConversationHistories.Where(history =>
                conversationHistoryIds.AsEnumerable().Contains(history.Id)
            )
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .NpcProfiles.Where(profile => ids.Contains(profile.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Reputations.Where(x => ids.Contains(x.CreatureId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .BuildingOwners.Where(x => ids.Contains(x.OwnerId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteRelationships(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        await context
            .Relationships.Where(x => ids.Contains(x.SubjectId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .Relationships.Where(x => ids.Contains(x.RelativeId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteKnowledge(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        await context
            .CreatureKnowledge.Where(x => ids.Contains(x.KnowerId))
            .ExecuteDeleteAsync(cancellationToken);

        await context
            .CreatureKnowledge.Where(x =>
                x.SubjectType == KnowledgeSubjectType.Creature && ids.Contains(x.SubjectId)
            )
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ClearOccupancy(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        await context
            .Set<Bed>()
            .Where(b => b.AssignedCreatureId != null && ids.Contains(b.AssignedCreatureId.Value))
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.AssignedCreatureId, (Guid?)null),
                cancellationToken
            );

        await context
            .Set<Bed>()
            .Where(b => b.OccupantId != null && ids.Contains(b.OccupantId.Value))
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.OccupantId, (Guid?)null),
                cancellationToken
            );

        await context
            .Set<Workstation>()
            .Where(w => w.AssignedCreatureId != null && ids.Contains(w.AssignedCreatureId.Value))
            .ExecuteUpdateAsync(
                s => s.SetProperty(w => w.AssignedCreatureId, (Guid?)null),
                cancellationToken
            );

        await context
            .Set<Workstation>()
            .Where(w => w.OccupantId != null && ids.Contains(w.OccupantId.Value))
            .ExecuteUpdateAsync(
                s => s.SetProperty(w => w.OccupantId, (Guid?)null),
                cancellationToken
            );

        await context
            .Set<Seat>()
            .Where(seat => seat.OccupantId != null && ids.Contains(seat.OccupantId.Value))
            .ExecuteUpdateAsync(
                s => s.SetProperty(seat => seat.OccupantId, (Guid?)null),
                cancellationToken
            );
    }
}
