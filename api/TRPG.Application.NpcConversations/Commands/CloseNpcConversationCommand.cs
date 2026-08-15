using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;

namespace TRPG.Application.NpcConversations.Commands;

public enum CloseNpcConversationOutcome
{
    Closed,
    NotOpen,
}

public class CloseNpcConversationCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string NpcName { get; init; }
    public required string Summary { get; init; }
}

public class CloseNpcConversationCommandHandler(
    GetGameSessionQueryHandler getGameSession,
    SetNpcConversationSummaryCommandHandler setNpcConversationSummary,
    UpdateGameSessionCommandHandler updateGameSession
)
{
    public async Task<CloseNpcConversationOutcome> Handle(
        CloseNpcConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (!snapshot.OpenConversationCreatureIdsByName.TryGetValue(command.NpcName, out var npcId))
        {
            return CloseNpcConversationOutcome.NotOpen;
        }

        await setNpcConversationSummary.Handle(
            new SetNpcConversationSummaryCommand
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

        return CloseNpcConversationOutcome.Closed;
    }
}
