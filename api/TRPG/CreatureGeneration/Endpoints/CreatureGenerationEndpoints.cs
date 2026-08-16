using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Creatures.Responses;

namespace TRPG.CreatureGeneration.Endpoints;

internal static class CreatureGenerationEndpoints
{
    public static void MapCreatureGenerationEndpoints(this WebApplication app)
    {
        app.MapGet("/creature-generation/options", GetOptions)
            .WithName("GetCreatureGenerationOptions");
    }

    private static Ok<CreatureGenerationOptionsResponse> GetOptions(
        IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
    )
    {
        var options = optionsSnapshot.Value;
        return TypedResults.Ok(
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
