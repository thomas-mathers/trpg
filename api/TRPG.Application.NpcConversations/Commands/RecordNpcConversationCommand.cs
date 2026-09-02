using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.NpcConversations.Commands;

public class RecordNpcConversationCommand
{
    public required string ConversationSummary { get; init; }
    public required IReadOnlyCollection<string> DurableFactsAdded { get; init; }
    public required IReadOnlyCollection<int> DurableFactsRemoved { get; init; }
    public required Guid NpcId { get; init; }
    public required IReadOnlyCollection<string> OpenThreadsAdded { get; init; }
    public required IReadOnlyCollection<int> OpenThreadsRemoved { get; init; }
    public required Guid PlayerId { get; init; }
    public required string Summary { get; init; }
    public required Guid WorldId { get; init; }
}

internal class RecordNpcConversationCommandHandler(INpcConversationsDbContext context)
    : ICommandHandler<RecordNpcConversationCommand>
{
    public async Task Handle(
        RecordNpcConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var history = await context.NpcConversationHistories.FirstOrDefaultAsync(
            item => item.CreatureId == command.PlayerId && item.NpcId == command.NpcId,
            cancellationToken
        );

        if (history == null)
        {
            history = new NpcConversationHistory
            {
                WorldId = command.WorldId,
                CreatureId = command.PlayerId,
                NpcId = command.NpcId,
                Summary = command.Summary,
            };
            context.NpcConversationHistories.Add(history);
        }
        else
        {
            history.Summary = command.Summary;
        }

        ApplyDurableFacts(history, command.DurableFactsAdded, command.DurableFactsRemoved);
        ApplyOpenThreads(history, command.OpenThreadsAdded, command.OpenThreadsRemoved);

        context.NpcConversations.Add(
            new NpcConversation
            {
                WorldId = command.WorldId,
                NpcConversationHistoryId = history.Id,
                Summary = command.ConversationSummary,
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyDurableFacts(
        NpcConversationHistory history,
        IReadOnlyCollection<string> added,
        IReadOnlyCollection<int> removed
    )
    {
        var activeIndices = history
            .DurableFacts.Select((fact, index) => (fact.IsRetracted, index))
            .Where(item => !item.IsRetracted)
            .Select(item => item.index)
            .ToArray();

        foreach (var ordinal in removed)
        {
            if (ordinal < 1 || ordinal > activeIndices.Length)
            {
                continue;
            }

            var listIndex = activeIndices[ordinal - 1];
            history.DurableFacts[listIndex] = history.DurableFacts[listIndex] with
            {
                IsRetracted = true,
            };
        }

        foreach (var text in added)
        {
            history.DurableFacts.Add(new NpcDurableFact(text));
        }
    }

    private static void ApplyOpenThreads(
        NpcConversationHistory history,
        IReadOnlyCollection<string> added,
        IReadOnlyCollection<int> removed
    )
    {
        var activeIndices = history
            .OpenThreads.Select((thread, index) => (thread.IsResolved, index))
            .Where(item => !item.IsResolved)
            .Select(item => item.index)
            .ToArray();

        foreach (var ordinal in removed)
        {
            if (ordinal < 1 || ordinal > activeIndices.Length)
            {
                continue;
            }

            var listIndex = activeIndices[ordinal - 1];
            history.OpenThreads[listIndex] = history.OpenThreads[listIndex] with
            {
                IsResolved = true,
            };
        }

        foreach (var text in added)
        {
            history.OpenThreads.Add(new NpcOpenThread(text));
        }
    }
}
