using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Abilities.Mappers;
using TRPG.Abilities.Responses;
using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Common.Handling;
using DataSkill = TRPG.Domain.Models.Skill;

namespace TRPG.Abilities.Endpoints;

internal static class AbilityEndpoints
{
    public static void MapAbilityEndpoints(this WebApplication app)
    {
        app.MapGet("/abilities/{skill}", GetAbilitiesBySkill).WithName("GetAbilitiesBySkill");
    }

    private static async Task<Ok<AbilitySummary[]>> GetAbilitiesBySkill(
        Skill skill,
        [FromServices]
            IQueryHandler<
            GetAbilitiesBySkillQuery,
            IReadOnlyCollection<Ability>
        > getAbilitiesBySkill,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getAbilitiesBySkill.Handle(
            new GetAbilitiesBySkillQuery { Skill = skill.ToDataModel() },
            cancellationToken
        );

        return TypedResults.Ok(abilities.Select(a => a.ToSummary()).ToArray());
    }
}
