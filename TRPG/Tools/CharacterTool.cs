using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Data.Models;
using TRPG.Services;

namespace TRPG.Tools;

internal record CharacterAttributesInfo(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Defense,
    int Mana,
    float MovementSpeed,
    float HpPercent,
    float MpPercent,
    float ApPercent,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance
);

internal record CharacterConditionInfo(string Type, int Value);

internal record CharacterModifierInfo(
    string Attribute,
    string Type,
    float Amount,
    int RemainingTurns
);

internal record CharacterSheetResult(
    string Name,
    int Level,
    CharacterAttributesInfo Attributes,
    IReadOnlyCollection<CharacterConditionInfo> Conditions,
    IReadOnlyCollection<CharacterModifierInfo> Modifiers
);

internal class CharacterTool : Tool, IInvokableTool
{
    private readonly CreatureService _creatureService;
    private readonly ILogger<CharacterTool> _logger;
    private readonly GameSession _session;

    public CharacterTool(
        GameSession session,
        CreatureService creatureService,
        ILogger<CharacterTool> logger
    )
    {
        _session = session;
        _creatureService = creatureService;
        _logger = logger;

        Function = new Function
        {
            Name = "character",
            Description =
                "Returns someone's attributes and active conditions/modifiers. Omit targetName to check the player's own character sheet, or pass the exact Name of a person from NearbyPeople to check theirs.",
            Parameters = new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>
                {
                    ["targetName"] = new()
                    {
                        Type = "string",
                        Description =
                            "The exact Name of a person from NearbyPeople, copied verbatim from the most recent look or move result. Omit to check the player's own character sheet.",
                    },
                },
                Required = new List<string>(),
            },
        };
    }

    public object? InvokeMethod(IDictionary<string, object?>? args)
    {
        var targetName =
            args != null && args.TryGetValue("targetName", out var raw) ? raw?.ToString() : null;

        _logger.LogInformation("[character] targetName={TargetName}", targetName ?? "(self)");
        var result = InvokeMethodAsync(targetName, CancellationToken.None).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogInformation("[character] result: {Result}", json);
        return json;
    }

    private async Task<object?> InvokeMethodAsync(
        string? targetName,
        CancellationToken cancellationToken
    )
    {
        var player = await _creatureService.GetById(_session.PlayerId, cancellationToken);

        Creature? target;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            target = player;
        }
        else
        {
            target = await _creatureService.GetByNameNearby(
                _session.WorldId,
                player!,
                targetName,
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

        return new CharacterSheetResult(
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
                attributes.HpPercent,
                attributes.MpPercent,
                attributes.ApPercent,
                attributes.PhysicalResistance,
                attributes.FireResistance,
                attributes.IceResistance,
                attributes.LightningResistance,
                attributes.PoisonResistance,
                attributes.MagicResistance
            ),
            target
                .ActiveConditions.Select(kvp => new CharacterConditionInfo(
                    kvp.Key.ToString(),
                    kvp.Value
                ))
                .ToArray(),
            target
                .ActiveModifiers.Select(m => new CharacterModifierInfo(
                    m.Attribute.ToString(),
                    m.Type.ToString(),
                    m.Amount,
                    m.RemainingTurns
                ))
                .ToArray()
        );
    }
}
