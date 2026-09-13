using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamAcceptQuestTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetQuestByIdQuery, Quest?> getQuestById,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<AcceptQuestCommand> acceptQuest
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, questId, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken
    )
    {
        var quest = await getQuestById.Handle(
            new GetQuestByIdQuery { Id = questId },
            cancellationToken
        );
        if (quest == null)
        {
            return new GameTurnPrompt.Reply("That quest is no longer available.");
        }

        await acceptQuest.Handle(
            new AcceptQuestCommand
            {
                PlayerId = session.PlayerId,
                QuestId = questId,
                WorldId = session.WorldId,
            },
            cancellationToken
        );

        var giver = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = quest.GiverId },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            $"The player just agreed to help {giver?.Name ?? "the quest giver"} with "
                + $"\"{quest.Name}\". Narrate a brief in-character acknowledgment or thanks in "
                + "one or two sentences. Do not restate the quest's objectives or reward."
        );
    }
}
