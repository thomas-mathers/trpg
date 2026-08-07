using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;

namespace TRPG.Application.Conversations.Commands;

internal enum CloseConversationOutcome
{
    Closed,
    NotOpen,
}

internal class CloseConversationCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string NpcName { get; init; }
    public required string Summary { get; init; }
}

internal class CloseConversationCommandHandler(
    GetGameSessionQueryHandler getGameSession,
    SetConversationSummaryCommandHandler setConversationSummary,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public async Task<CloseConversationOutcome> Handle(
        CloseConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (!snapshot.OpenConversationCreatureIdsByName.TryGetValue(command.NpcName, out var npcId))
        {
            return CloseConversationOutcome.NotOpen;
        }

        await setConversationSummary.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = command.WorldId,
                CreatureId = command.PlayerId,
                NpcId = npcId,
                Summary = command.Summary,
            },
            cancellationToken
        );

        snapshot.OpenConversationCreatureIdsByName.Remove(command.NpcName);
        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = command.SessionId,
                OpenConversationCreatureIdsByName = snapshot.OpenConversationCreatureIdsByName,
            },
            cancellationToken
        );

        return CloseConversationOutcome.Closed;
    }
}
