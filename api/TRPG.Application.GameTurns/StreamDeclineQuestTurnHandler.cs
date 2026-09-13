using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamDeclineQuestTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetQuestByIdQuery, Quest?> getQuestById,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(questId, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
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

        var giver = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = quest.GiverId },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            $"The player just decided not to help {giver?.Name ?? "the quest giver"} with "
                + $"\"{quest.Name}\" right now. Narrate a brief, mild in-character reaction in "
                + "one or two sentences — not final or hostile, since the player can still accept it later."
        );
    }
}
