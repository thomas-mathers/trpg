using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Client;

internal sealed class NarrationRenderer(ILogger logger, IReadOnlyList<NamedEntity> namedEntities)
{
    private static readonly IReadOnlyDictionary<EntityType, string> EntityTypeChipStyles =
        new Dictionary<EntityType, string>
        {
            [EntityType.Creature] = $"{Theme.ChipForeground} on {Theme.CreatureAccent}",
            [EntityType.Building] = $"{Theme.ChipForeground} on {Theme.BuildingAccent}",
            [EntityType.District] = $"{Theme.ChipForeground} on {Theme.DistrictAccent}",
            [EntityType.World] = $"{Theme.ChipForeground} on {Theme.WorldAccent}",
            [EntityType.Country] = $"{Theme.ChipForeground} on {Theme.CountryAccent}",
            [EntityType.State] = $"{Theme.ChipForeground} on {Theme.StateAccent}",
            [EntityType.City] = $"{Theme.ChipForeground} on {Theme.CityAccent}",
        };

    public async Task<bool> TryRender(IAsyncEnumerable<string> tokens)
    {
        try
        {
            var prefix = "";

            await foreach (var token in tokens)
            {
                foreach (var c in token)
                {
                    var isPrefix = namedEntities.Any(entity =>
                        entity.Name.StartsWith(prefix + c, StringComparison.OrdinalIgnoreCase)
                    );
                    if (!isPrefix)
                    {
                        PrintPrefix(prefix);
                        prefix = "";
                    }

                    prefix += c;
                }
            }

            PrintPrefix(prefix);

            return true;
        }
        catch (Exception ex) when (ex is HubException or IOException)
        {
            logger.LogError(ex, "[game] turn failed while streaming");
            AnsiConsole.AnnounceError($"[[Turn failed: {ex.Message.EscapeMarkup()}]]");
            return false;
        }
    }

    private void PrintPrefix(string prefix)
    {
        var match = namedEntities.FirstOrDefault(entity =>
            entity.Name.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        );
        if (match != null)
        {
            PrintEntityChip(match.Name, match.Type);
        }
        else
        {
            PrintNarration(prefix);
        }
    }

    private static void PrintNarration(string text) =>
        AnsiConsole.Markup($"[italic]{text.EscapeMarkup()}[/]");

    private static void PrintEntityChip(string name, EntityType type)
    {
        var style = EntityTypeChipStyles.GetValueOrDefault(
            type,
            $"{Theme.ChipForeground} on {Theme.NeutralAccent}"
        );
        AnsiConsole.Markup($"[{style}] {name.EscapeMarkup()} [/]");
    }
}
