using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Data.Models;

namespace TRPG.Application.Tools;

internal record CharacterAttributesInfo(
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

internal record CharacterSheetResult(string Name, int Level, CharacterAttributesInfo Attributes);

internal class CharacterTool(
    GameSession session,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreatureByNameNearbyQueryHandler getCreatureByNameNearby,
    ILogger<CharacterTool> logger
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
            new GetCreatureByIdQuery { Id = session.PlayerId },
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
                    WorldId = session.WorldId,
                    Player = player!,
                    Name = targetName,
                },
                cancellationToken
            );

            if (target == null)
            {
                return new
                {
                    Error = $"No one named '{targetName}' found nearby. Call look to see who's around.",
                };
            }
        }

        var attributes = target!.Attributes;

        var result = new CharacterSheetResult(
            target.Name,
            target.Level,
            new CharacterAttributesInfo(
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
