using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class NpcConversationService(TrpgDbContext context) {
    public async Task<string> GetSummary(Guid personId, Guid npcId, CancellationToken cancellationToken = default) {
        var conversation = await context.NpcConversations
            .FirstOrDefaultAsync(c => c.PersonId == personId && c.NpcId == npcId, cancellationToken);

        return conversation?.Summary ?? "";
    }

    public async Task SetSummary(Guid worldId, Guid personId, Guid npcId, string summary,
        CancellationToken cancellationToken = default) {
        var conversation = await context.NpcConversations
            .FirstOrDefaultAsync(c => c.PersonId == personId && c.NpcId == npcId, cancellationToken);

        if (conversation == null) {
            context.NpcConversations.Add(new NpcConversation {
                WorldId = worldId,
                PersonId = personId,
                NpcId = npcId,
                Summary = summary
            });
        } else {
            conversation.Summary = summary;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
