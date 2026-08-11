using TRPG.Application.Common.Events;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;

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
    DomainEventTransactionRunner domainEventTransactions,
    GetGameSessionQueryHandler getGameSession,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public Task<OpenConversationOutcome> Handle(
        OpenConversationCommand command,
        CancellationToken cancellationToken = default
    ) => domainEventTransactions.Run(command, HandleWithinTransaction, cancellationToken);

    private async Task<GameActionResult<OpenConversationOutcome>> HandleWithinTransaction(
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
            return new GameActionResult<OpenConversationOutcome>(
                Result: OpenConversationOutcome.AlreadyOpen,
                Events: []
            );
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

        return new GameActionResult<OpenConversationOutcome>(
            Result: OpenConversationOutcome.Opened,
            Events:
            [
                new ConversationStartedEvent(
                    PlayerId: snapshot.PlayerId,
                    WorldId: snapshot.WorldId,
                    CreatureId: command.NpcId
                ),
            ]
        );
    }
}
