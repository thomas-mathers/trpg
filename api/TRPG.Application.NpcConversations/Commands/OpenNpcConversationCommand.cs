using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Events;
using TRPG.Data;

namespace TRPG.Application.NpcConversations.Commands;

public enum OpenNpcConversationOutcome
{
    Opened,
    AlreadyOpen,
}

public class OpenNpcConversationCommand
{
    public required Guid SessionId { get; init; }
    public required Guid NpcId { get; init; }
    public required string NpcName { get; init; }
}

public class OpenNpcConversationCommandHandler(
    TrpgDbContext context,
    ConversationStartedQuestEventHandler conversationStartedQuestEvents,
    GetGameSessionQueryHandler getGameSession,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public async Task<OpenNpcConversationOutcome> Handle(
        OpenNpcConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (snapshot.OpenConversationCreatureIdsByName.ContainsKey(command.NpcName))
        {
            await transaction.CommitAsync(cancellationToken);
            return OpenNpcConversationOutcome.AlreadyOpen;
        }

        snapshot.OpenConversationCreatureIdsByName[command.NpcName] = command.NpcId;
        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = command.SessionId,
                OpenConversationCreatureIdsByName = snapshot.OpenConversationCreatureIdsByName,
            },
            cancellationToken
        );

        await conversationStartedQuestEvents.Handle(
            new ConversationStartedQuestEvent(
                PlayerId: snapshot.PlayerId,
                WorldId: snapshot.WorldId,
                CreatureId: command.NpcId
            ),
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        return OpenNpcConversationOutcome.Opened;
    }
}
