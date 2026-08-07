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
    GetGameSessionQueryHandler getGameSession,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public async Task<OpenConversationOutcome> Handle(
        OpenConversationCommand command,
        CancellationToken cancellationToken = default
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

        return OpenConversationOutcome.Opened;
    }
}
