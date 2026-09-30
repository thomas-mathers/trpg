using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Domain;

namespace TRPG.GameSessions.Commands;

internal class ReleaseOpenInteractionsCommand
{
    public required Guid WorldId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class ReleaseOpenInteractionsCommandHandler(
    ICommandHandler<ClearNonEncounterEngagementsCommand> clearNonEncounterEngagements,
    ICommandHandler<ClearWorldOpenNpcConversationsCommand> clearOpenNpcConversations
) : ICommandHandler<ReleaseOpenInteractionsCommand>
{
    public async Task Handle(
        ReleaseOpenInteractionsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await clearNonEncounterEngagements.Handle(
            new ClearNonEncounterEngagementsCommand
            {
                WorldId = command.WorldId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        await clearOpenNpcConversations.Handle(
            new ClearWorldOpenNpcConversationsCommand { WorldId = command.WorldId },
            cancellationToken
        );
    }
}
