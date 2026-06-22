using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class NpcConversationService(TrpgDbContext context) {
    public async Task AddMessage(Guid worldId, Guid fromId, Guid toId, string message,
        CancellationToken cancellationToken = default) {
        var conversation = await context.NpcConversations
            .FirstOrDefaultAsync(c =>
                (c.PersonId == fromId && c.NpcId == toId) ||
                (c.PersonId == toId && c.NpcId == fromId), cancellationToken);

        if (conversation == null) {
            conversation = new NpcConversation {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                PersonId = fromId,
                NpcId = toId,
                Summary = "",
                LastSummarizedIndex = null
            };
            context.NpcConversations.Add(conversation);
            await context.SaveChangesAsync(cancellationToken);
        }

        var lastIndex = await context.NpcChatMessages
            .Where(m => m.ConversationId == conversation.Id)
            .MaxAsync(m => (int?) m.Index, cancellationToken) ?? -1;

        context.NpcChatMessages.Add(new NpcChatMessage {
            ConversationId = conversation.Id,
            Index = lastIndex + 1,
            SenderId = fromId,
            RecipientId = toId,
            Message = message,
            Date = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReadOnlyCollection<NpcChatMessage>> GetAllMessages(Guid personId, Guid npcId,
        int startingMessageIndex, CancellationToken cancellationToken = default) {
        var conversation = await context.NpcConversations
            .FirstOrDefaultAsync(c => c.PersonId == personId && c.NpcId == npcId, cancellationToken);

        if (conversation == null) {
            return new ReadOnlyCollection<NpcChatMessage>([]);
        }

        var list = await context.NpcChatMessages
            .Where(m => m.ConversationId == conversation.Id && m.Index >= startingMessageIndex)
            .OrderBy(m => m.Index)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task UpdateSummary(Guid personId, Guid npcId, string summary,
        CancellationToken cancellationToken = default) {
        var conversation = await context.NpcConversations
            .FirstOrDefaultAsync(c => c.PersonId == personId && c.NpcId == npcId, cancellationToken);

        if (conversation == null) {
            throw new InvalidOperationException($"No conversation found between person {personId} and NPC {npcId}.");
        }

        var lastIndex = await context.NpcChatMessages
            .Where(m => m.ConversationId == conversation.Id)
            .MaxAsync(m => (int?) m.Index, cancellationToken) ?? -1;

        if (lastIndex < 0) {
            throw new InvalidOperationException("Cannot summarize a conversation with no messages.");
        }

        conversation.Summary = summary;
        conversation.LastSummarizedIndex = lastIndex;

        await context.SaveChangesAsync(cancellationToken);
    }
}