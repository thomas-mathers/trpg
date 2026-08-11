using TRPG.Application.Common.Events;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Quests;
using TRPG.Data;

namespace TRPG.Application.Conversations.Commands;

internal enum OpenConversationOutcome
{
    Opened,
    AlreadyOpen,
}

internal class OpenConversationCommand
{
    public required Guid SessionId { get; init; }
    public required Guid NpcId { get; init; }
    public required string NpcName { get; init; }
}

internal class OpenConversationCommandHandler(
    TrpgDbContext context,
    QuestEventHandler questEvents,
    GetGameSessionQueryHandler getGameSession,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public async Task<OpenConversationOutcome> Handle(
        OpenConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var outcome = await HandleWithinTransaction(command, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }

    private async Task<OpenConversationOutcome> HandleWithinTransaction(
        OpenConversationCommand command,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (snapshot.OpenConversationCreatureIdsByName.ContainsKey(command.NpcName))
        {
            return OpenConversationOutcome.AlreadyOpen;
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

        await questEvents.Handle(
            new ConversationStartedEvent(
                PlayerId: snapshot.PlayerId,
                WorldId: snapshot.WorldId,
                CreatureId: command.NpcId
            ),
            cancellationToken
        );
        return OpenConversationOutcome.Opened;
    }
}
