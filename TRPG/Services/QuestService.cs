using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class QuestService(TrpgDbContext context)
{
    public async Task AssignQuest(Guid questId, Guid personId, CancellationToken cancellationToken = default)
    {
        var quest = await context.Quests.FindAsync([questId], cancellationToken);
        
        if (quest == null)
        {
            throw new InvalidOperationException($"Quest {questId} not found.");
        }

        if (quest.PrerequisiteQuestIds.Count > 0)
        {
            var completedQuestIds = await context.PersonQuests
                .Where(pq => pq.PersonId == personId && pq.Status == QuestStatus.Completed)
                .Select(pq => pq.QuestId)
                .ToListAsync(cancellationToken);

            if (!quest.PrerequisiteQuestIds.All(completedQuestIds.Contains))
                throw new InvalidOperationException("Quest prerequisites not met.");
        }

        context.PersonQuests.Add(new PersonQuest
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            QuestId = questId,
            Status = QuestStatus.Accepted
        });

        var objectives = await context.QuestObjectives
            .Where(o => o.QuestId == questId)
            .ToListAsync(cancellationToken);

        foreach (var objective in objectives)
        {
            context.PersonQuestObjectives.Add(new PersonQuestObjective
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                ObjectiveId = objective.Id,
                Amount = 0
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Quest?> GetById(Guid questId, CancellationToken cancellationToken = default)
        => await context.Quests.FindAsync([questId], cancellationToken);

    public async Task<List<Quest>> GetAllQuestsByGiverId(Guid giverId, CancellationToken cancellationToken = default)
        => await context.Quests
            .Where(q => q.GiverId == giverId)
            .ToListAsync(cancellationToken);

    public async Task<List<QuestObjective>> GetAllQuestObjectivesByQuestId(Guid questId, CancellationToken cancellationToken = default)
        => await context.QuestObjectives
            .Where(o => o.QuestId == questId)
            .ToListAsync(cancellationToken);

    public async Task<List<PersonQuest>> GetAllQuestsByPersonId(Guid personId, CancellationToken cancellationToken = default)
        => await context.PersonQuests
            .Include(pq => pq.Quest)
            .Where(pq => pq.PersonId == personId)
            .ToListAsync(cancellationToken);

    public async Task<List<PersonQuestObjective>> GetAllQuestObjectivesByPersonId(Guid personId, CancellationToken cancellationToken = default)
        => await context.PersonQuestObjectives
            .Include(po => po.Objective)
            .Where(po => po.PersonId == personId)
            .ToListAsync(cancellationToken);

    public async Task ProgressObjective(Guid personId, Guid questObjectiveId, int amount, CancellationToken cancellationToken = default)
    {
        var objective = await context.PersonQuestObjectives
            .FirstOrDefaultAsync(o => o.PersonId == personId && o.ObjectiveId == questObjectiveId, cancellationToken);

        if (objective == null) throw new InvalidOperationException($"Quest objective {questObjectiveId} not found for person {personId}.");
        objective.Amount += amount;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetQuestStatus(Guid questId, Guid personId, QuestStatus status, CancellationToken cancellationToken = default)
    {
        var personQuest = await context.PersonQuests
            .FirstOrDefaultAsync(pq => pq.QuestId == questId && pq.PersonId == personId, cancellationToken);

        if (personQuest == null)
        {
            throw new InvalidOperationException($"Quest {questId} not found for person {personId}.");
        }
        
        personQuest.Status = status;
        await context.SaveChangesAsync(cancellationToken);
    }
}
