using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Tools;

internal class LookTool : Tool, IInvokableTool
{
    private readonly GetCreatureByIdQueryHandler _getCreatureById;
    private readonly GetSceneWithCatchUpQueryHandler _getSceneWithCatchUp;
    private readonly ILogger<LookTool> _logger;
    private readonly GameSession _session;

    public LookTool(
        GameSession session,
        GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
        GetCreatureByIdQueryHandler getCreatureById,
        ILogger<LookTool> logger
    )
    {
        _session = session;
        _getSceneWithCatchUp = getSceneWithCatchUp;
        _getCreatureById = getCreatureById;
        _logger = logger;

        Function = new Function
        {
            Name = "look",
            Description =
                "Returns everything currently observable at the player's location: CurrentDate (Year, MonthName, Day, WeekdayName, and a 24-hour Hour where 0 is midnight); the current region; the building and room (with its exits) if indoors; nearby props and people; and nearby buildings (only populated outdoors — empty indoors because you can't see outside from in here, not because the city has no buildings). Call this before narrating any location, and again after anything might have changed what's nearby.",
            Parameters = new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>(),
                Required = new List<string>(),
            },
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        _logger.LogInformation("[look] tool invoked");
        var result = InvokeMethodAsync(CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[look] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(CancellationToken cancellationToken)
    {
        var currentDate = GameClock.GetCurrentInGameDate(_session);
        var player = await _getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = _session.PlayerId },
            cancellationToken
        );
        var result = await _getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                Session = _session,
                RoomId = player!.RoomId,
                DistrictId = player.DistrictId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        _session.DidSceneRefreshThisTurn = true;
        return result;
    }
}
