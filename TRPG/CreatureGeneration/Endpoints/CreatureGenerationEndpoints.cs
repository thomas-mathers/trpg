using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Contracts.Creatures.Responses;

namespace TRPG.CreatureGeneration.Endpoints;

internal static class CreatureGenerationEndpoints
{
    public static void MapCreatureGenerationEndpoints(this WebApplication app)
    {
        app.MapGet("/creature-generation/options", GetOptions);
    }

    private static IResult GetOptions(IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot)
    {
        var options = optionsSnapshot.Value;
        return Results.Ok(
            new CreatureGenerationOptionsResponse(
                options.PointsPerLevel,
                new BaseAttributesResponse(
                    options.BaseAttributes.Strength,
                    options.BaseAttributes.Defense,
                    options.BaseAttributes.Dexterity,
                    options.BaseAttributes.Endurance,
                    options.BaseAttributes.Stamina,
                    options.BaseAttributes.Mana,
                    options.BaseAttributes.Intelligence
                )
            )
        );
    }
}
