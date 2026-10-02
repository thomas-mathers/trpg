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
using TRPG.Application.RoomBookings.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.RoomBookings.Tools;

internal class ReturnRoomKeyTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    ICommandHandler<ReturnRoomKeyCommand, ReturnRoomKeyResult> returnRoomKey,
    ICommandHandler<
        ConfrontOverdueRoomKeyCommand,
        ConfrontOverdueRoomKeyResult
    > confrontOverdueRoomKey,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
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
        var npc = await FindNpc(npcName, player!.LocationId, cancellationToken);
        if (npc == null)
        {
            return new ToolError($"No one named '{npcName}' found nearby.");
        }

        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        var buildingId = await GetInnBuildingId(player.LocationId, cancellationToken);
        var confrontation = await ConfrontOverdueKey(
            player.LocationId,
            buildingId,
            gameTime,
            cancellationToken
        );
        if (confrontation.Encounter != null)
        {
            await PublishConfrontation(confrontation.Encounter, gameTime, cancellationToken);
            var confrontedResult = new
            {
                Confronted = true,
                Instruction = "The innkeeper has confronted the player about the overdue key. Stop the response without narration.",
            };
            LogResult(stopwatch, confrontedResult);
            return confrontedResult;
        }

        var returnResult = await returnRoomKey.Handle(
            new ReturnRoomKeyCommand
            {
                PlayerId = turnContext.PlayerId,
                WorldId = turnContext.WorldId,
                LocationId = player.LocationId,
            },
            cancellationToken
        );

        object? result = returnResult.Outcome switch
        {
            ReturnRoomKeyOutcome.NoActiveBooking => new ToolError(
                "The player isn't currently renting a room here."
            ),
            _ => new { Returned = true },
        };

        LogResult(stopwatch, result);
        return result;
    }

    private Task<Creature?> FindNpc(
        string npcName,
        Guid locationId,
        CancellationToken cancellationToken
    ) =>
        getCreatureByNameAtLocation.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = turnContext.WorldId,
                LocationId = locationId,
                Name = npcName,
            },
            cancellationToken
        );

    private async Task<Guid> GetInnBuildingId(Guid locationId, CancellationToken cancellationToken)
    {
        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );
        if (building is not { BuildingType: BuildingType.Inn })
        {
            throw new InvalidOperationException($"Location {locationId} is not inside an Inn.");
        }
        return building.Id;
    }

    private Task<ConfrontOverdueRoomKeyResult> ConfrontOverdueKey(
        Guid locationId,
        Guid buildingId,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        confrontOverdueRoomKey.Handle(
            new ConfrontOverdueRoomKeyCommand
            {
                PlayerId = turnContext.PlayerId,
                WorldId = turnContext.WorldId,
                GameTime = gameTime,
                LocationId = locationId,
                BuildingId = buildingId,
            },
            cancellationToken
        );

    private Task PublishConfrontation(
        TheftEncounter encounter,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = turnContext.PlayerId,
                Encounter = encounter,
                GameTime = gameTime,
            },
            cancellationToken
        );

    private void LogResult(Stopwatch stopwatch, object? result) =>
        logger.LogInformation(
            "[perf] [return_key] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
}
