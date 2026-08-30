using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.Buildings.Tools;

internal class BookRoomTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    ICommandHandler<BookRoomCommand, BookRoomResult> bookRoom,
    ILogger<BookRoomTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("book_room")]
    [Description(
        "Call this only after the player has explicitly confirmed they want to rent a room for the night from an innkeeper — never merely because they asked about rates or availability. Renting a room costs 5 gold, charged immediately, and covers one night."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the innkeeper you're speaking with, copied verbatim from start_conversation."
        )]
            string npcName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[book_room] npcName={NpcName}", npcName);
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

        var bookingResult = await bookRoom.Handle(
            new BookRoomCommand
            {
                PlayerId = turnContext.PlayerId,
                WorldId = turnContext.WorldId,
                SessionId = turnContext.SessionId,
                LocationId = player.LocationId,
            },
            cancellationToken
        );

        object? result = bookingResult.Outcome switch
        {
            BookRoomOutcome.NoVacancy => new ToolError("Every room here is taken right now."),
            BookRoomOutcome.InsufficientGold => new ToolError(
                "The player doesn't have enough gold to cover the room."
            ),
            _ => new
            {
                Booked = true,
                bookingResult.RoomName,
                bookingResult.GoldCharged,
            },
        };

        logger.LogInformation(
            "[perf] [book_room] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
        return result;
    }
}
