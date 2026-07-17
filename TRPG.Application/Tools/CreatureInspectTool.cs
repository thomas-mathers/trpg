using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.Common.Tools;
using TRPG.Data.Models;

namespace TRPG.Application.Tools;

internal record CreatureAttributesInfo(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Defense,
    int Mana,
    float MovementSpeed,
    int MaximumHp,
    int MaximumMp,
    int MaximumAp,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance
);

internal record CreatureInspectResult(string Name, int Level, CreatureAttributesInfo Attributes);

internal class CreatureInspectTool(
    GameTurnContext turnContext,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreatureByNameNearbyQueryHandler getCreatureByNameNearby,
    ILogger<CreatureInspectTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("character")]
    [Description(
        "Returns someone's attributes. Omit targetName to check the player's own character sheet, or pass the exact Name of a person from NearbyPeople to check theirs."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a person from NearbyPeople, copied verbatim from the most recent look or move result. Omit to check the player's own character sheet."
        )]
            string? targetName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[character] targetName={TargetName}", targetName ?? "(self)");
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        Creature? target;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            target = player;
        }
        else
        {
            target = await getCreatureByNameNearby.Handle(
                new GetCreatureByNameNearbyQuery
                {
                    WorldId = turnContext.WorldId,
                    Player = player!,
                    Name = targetName,
                },
                cancellationToken
            );

            if (target == null)
            {
                return new ToolError(
                    $"No one named '{targetName}' found nearby. Call look to see who's around."
                );
            }
        }

        var attributes = target!.Attributes;

        var result = new CreatureInspectResult(
            target.Name,
            target.Level,
            new CreatureAttributesInfo(
                attributes.Strength,
                attributes.Dexterity,
                attributes.Intelligence,
                attributes.Endurance,
                attributes.Stamina,
                attributes.Defense,
                attributes.Mana,
                attributes.MovementSpeed,
                attributes.MaximumHp,
                attributes.MaximumMp,
                attributes.MaximumAp,
                attributes.PhysicalResistance,
                attributes.FireResistance,
                attributes.IceResistance,
                attributes.LightningResistance,
                attributes.PoisonResistance,
                attributes.MagicResistance
            )
        );

        logger.LogInformation(
            "[perf] [character] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
