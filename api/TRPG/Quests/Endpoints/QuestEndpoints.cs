using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Contracts.Quests.Requests;
using TRPG.Contracts.Quests.Responses;

namespace TRPG.Quests.Endpoints;

internal static class QuestEndpoints
{
    public static void MapQuestEndpoints(this WebApplication app)
    {
        app.MapGet("/players/{playerId:guid}/quests", GetQuestJournal).WithName("GetQuestJournal");
        app.MapPost("/players/{playerId:guid}/quests/{questId:guid}/accept", AcceptQuest)
            .WithName("AcceptQuest")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        app.MapPost("/players/{playerId:guid}/quests/{questId:guid}/complete", CompleteQuest)
            .WithName("CompleteQuest")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        app.MapPut("/players/{playerId:guid}/quests/{questId:guid}/tracking", SetTracking)
            .WithName("SetQuestTracking")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<Ok<QuestJournalEntrySnapshot[]>> GetQuestJournal(
        Guid playerId,
        Guid worldId,
        GetQuestJournalQueryHandler getQuestJournal,
        CancellationToken cancellationToken
    )
    {
        var quests = await getQuestJournal.Handle(
            new GetQuestJournalQuery { PlayerId = playerId, WorldId = worldId },
            cancellationToken
        );
        return TypedResults.Ok(
            quests
                .Select(quest => new QuestJournalEntrySnapshot(
                    quest.Id,
                    quest.Name,
                    quest.Description,
                    quest.GoldReward,
                    quest.Status.ToString(),
                    quest.IsTracked,
                    quest
                        .Objectives.Select(objective => new QuestObjectiveProgressSnapshot(
                            objective.Name,
                            objective.Description,
                            objective.Amount,
                            objective.RequiredAmount,
                            objective.LocationName
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
    }

    private static async Task<NoContent> AcceptQuest(
        Guid playerId,
        Guid questId,
        Guid worldId,
        AcceptQuestCommandHandler acceptQuest,
        CancellationToken cancellationToken
    )
    {
        await acceptQuest.Handle(
            new AcceptQuestCommand
            {
                PlayerId = playerId,
                QuestId = questId,
                WorldId = worldId,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> CompleteQuest(
        Guid playerId,
        Guid questId,
        Guid worldId,
        CompleteQuestCommandHandler completeQuest,
        CancellationToken cancellationToken
    )
    {
        await completeQuest.Handle(
            new CompleteQuestCommand
            {
                PlayerId = playerId,
                QuestId = questId,
                WorldId = worldId,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetTracking(
        Guid playerId,
        Guid questId,
        Guid worldId,
        SetQuestTrackingRequest request,
        SetQuestTrackingCommandHandler setQuestTracking,
        CancellationToken cancellationToken
    )
    {
        await setQuestTracking.Handle(
            new SetQuestTrackingCommand
            {
                PlayerId = playerId,
                QuestId = questId,
                WorldId = worldId,
                IsTracked = request.IsTracked,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }
}
