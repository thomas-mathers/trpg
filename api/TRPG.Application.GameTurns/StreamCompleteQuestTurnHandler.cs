using TRPG.Application.Books.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamCompleteQuestTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetQuestByIdQuery, Quest?> getQuestById,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetReportFactIdsForQuestQuery, IReadOnlyCollection<Guid>> getReportFactIds,
    IQueryHandler<GetFactByIdQuery, Fact?> getFactById,
    ICommandHandler<CompleteQuestCommand> completeQuest
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

        var reportFactIds = await getReportFactIds.Handle(
            new GetReportFactIdsForQuestQuery(questId),
            cancellationToken
        );
        var reportFacts = await Task.WhenAll(
            reportFactIds.Select(factId =>
                getFactById.Handle(new GetFactByIdQuery { FactId = factId }, cancellationToken)
            )
        );

        await completeQuest.Handle(
            new CompleteQuestCommand
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
            $"The player just turned in \"{quest.Name}\" to {giver?.Name ?? "the quest giver"} "
                + "and received their reward. Narrate a brief in-character reaction from "
                + $"{giver?.Name ?? "the quest giver"} to what was turned in — their gratitude, "
                + "relief, or reaction to the outcome — in one or two sentences. Do not restate "
                + "the reward amount or objectives."
                + FormatReportedFacts(reportFacts)
        );
    }

    private static string FormatReportedFacts(IReadOnlyCollection<Fact?> facts) =>
        facts.Where(fact => fact is not null).Select(fact => fact!).ToArray()
            is { Length: > 0 } reported
            ? " The player reported these facts, which the recipient now knows: "
                + string.Join("; ", reported.Select(fact => $"{fact.Subject}: {fact.Value}"))
                + ". React specifically to them."
            : "";
}
