using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TRPG.Data;
using ChatMessageRow = TRPG.Data.Models.ChatMessage;

namespace TRPG.Application.Chat.Commands;

public class AppendChatMessagesCommand
{
    public required Guid SessionId { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
}

public class AppendChatMessagesCommandHandler(
    TrpgDbContext context,
    ILogger<AppendChatMessagesCommandHandler> logger
)
{
    public async Task<int> Handle(
        AppendChatMessagesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var nextOrdinal =
            await context
                .ChatMessages.Where(m => m.SessionId == command.SessionId)
                .MaxAsync(m => (int?)m.Ordinal, cancellationToken)
            ?? -1;
        nextOrdinal++;

        var firstOrdinal = nextOrdinal;
        foreach (var message in command.Messages)
        {
            context.ChatMessages.Add(
                new ChatMessageRow
                {
                    SessionId = command.SessionId,
                    Ordinal = nextOrdinal,
                    Role = message.Role.Value,
                    MessageJson = JsonSerializer.Serialize(message, AIJsonUtilities.DefaultOptions),
                }
            );
            nextOrdinal++;
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "[perf] AppendChatMessages saved {Count} message(s) in {ElapsedMs}ms",
            command.Messages.Count,
            stopwatch.ElapsedMilliseconds
        );

        return firstOrdinal;
    }
}
