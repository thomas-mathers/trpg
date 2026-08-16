using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Events;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Results;

namespace TRPG.Application.Quests.Commands;

public class RequestQuestDialogCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid GiverId { get; init; }
    public required string QuestName { get; init; }
}

public record QuestDialogRequestResult(bool IsAvailable);

internal class RequestQuestDialogCommandHandler(
    IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult> getQuestInteractions,
    IGameClientEventSink gameEvents
) : ICommandHandler<RequestQuestDialogCommand, QuestDialogRequestResult>
{
    public async Task<QuestDialogRequestResult> Handle(
        RequestQuestDialogCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var interactions = await getQuestInteractions.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = command.GiverId,
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
            },
            cancellationToken
        );

        var availableQuest = interactions.AvailableQuests.FirstOrDefault(quest =>
            quest.Name == command.QuestName
        );
        if (availableQuest is not null)
        {
            gameEvents.Enqueue(
                new QuestDialogRequestedEvent(
                    command.WorldId,
                    availableQuest,
                    QuestDialogMode.Offer
                )
            );
            return new QuestDialogRequestResult(IsAvailable: true);
        }

        var readyQuest = interactions.ReadyToCompleteQuests.FirstOrDefault(quest =>
            quest.Name == command.QuestName
        );
        if (readyQuest is not null)
        {
            gameEvents.Enqueue(
                new QuestDialogRequestedEvent(command.WorldId, readyQuest, QuestDialogMode.TurnIn)
            );
            return new QuestDialogRequestResult(IsAvailable: true);
        }

        return new QuestDialogRequestResult(IsAvailable: false);
    }
}
