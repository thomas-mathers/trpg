using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.GameTurns.Tools;

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

internal record CreatureInspectResult(
    string Name,
    int Level,
    int CurrentHp,
    int CurrentAp,
    int CurrentMp,
    CreatureAttributesInfo Attributes
);

internal class CreatureInspectTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
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
            target = await getCreatureByNameAtLocation.Handle(
                new GetCreatureByNameAtLocationQuery
                {
                    WorldId = turnContext.WorldId,
                    LocationId = player!.LocationId,
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

        var result = new CreatureInspectResult(
            target!.Name,
            target.Level,
            target.CurrentHp,
            target.CurrentAp,
            target.CurrentMp,
            new CreatureAttributesInfo(
                target.Strength,
                target.Dexterity,
                target.Intelligence,
                target.Endurance,
                target.Stamina,
                target.Defense,
                target.Mana,
                target.MovementSpeed,
                target.MaximumHp,
                target.MaximumMp,
                target.MaximumAp,
                target.PhysicalResistance,
                target.FireResistance,
                target.IceResistance,
                target.LightningResistance,
                target.PoisonResistance,
                target.MagicResistance
            )
        );

        logger.LogInformation(
            "[perf] [character] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, TRPG.Contracts.TrpgJsonOptions.Default)
        );
        return result;
    }
}
