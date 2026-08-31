using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.Buildings.Tools;

internal class ReturnRoomKeyTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    ICommandHandler<ReturnRoomKeyCommand, ReturnRoomKeyResult> returnRoomKey,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ILogger<ReturnRoomKeyTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("return_key")]
    [Description(
        "Call this when the player explicitly hands their room key back to an innkeeper to check out. If the player is past their checkout time and never returned it, the innkeeper confronts them instead of accepting a plain return — in that case stop the response without narrating the checkout as if it went smoothly."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the innkeeper you're speaking with, copied verbatim from start_conversation."
        )]
            string npcName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[return_key] npcName={NpcName}", npcName);
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );
        var npc = await getCreatureByNameAtLocation.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = turnContext.WorldId,
                LocationId = player!.LocationId,
                Name = npcName,
            },
            cancellationToken
        );
        if (npc == null)
        {
            return new ToolError(
                $"No one named '{npcName}' found nearby. Call look to see who's around."
            );
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        var returnResult = await returnRoomKey.Handle(
            new ReturnRoomKeyCommand
            {
                PlayerId = turnContext.PlayerId,
                WorldId = turnContext.WorldId,
                Playtime = playtime,
                LocationId = player.LocationId,
            },
            cancellationToken
        );

        object? result;
        if (returnResult.Outcome == ReturnRoomKeyOutcome.Overdue)
        {
            await publishEncounterStarted.Handle(
                new PublishEncounterStartedCommand
                {
                    PlayerId = turnContext.PlayerId,
                    Encounter = returnResult.Encounter,
                },
                cancellationToken
            );
            result = new
            {
                Confronted = true,
                Instruction = "The innkeeper has confronted the player about the overdue key. Stop the response without narration.",
            };
        }
        else
        {
            result = returnResult.Outcome switch
            {
                ReturnRoomKeyOutcome.NoActiveBooking => new ToolError(
                    "The player isn't currently renting a room here."
                ),
                _ => new { Returned = true },
            };
        }

        logger.LogInformation(
            "[perf] [return_key] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
        return result;
    }
}
