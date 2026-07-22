using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Contracts.Inventory.Responses;

namespace TRPG.Creatures.Endpoints;

internal static class CreatureEndpoints
{
    public static void MapCreatureEndpoints(this WebApplication app)
    {
        app.MapGet("/creatures/{creatureId:guid}/abilities", GetAbilities);
        app.MapGet("/creatures/{creatureId:guid}/items", GetUsableItems);
        app.MapGet("/creatures/{creatureId:guid}/attribute-points", GetAttributePoints);
        app.MapPost(
            "/creatures/{creatureId:guid}/attribute-points/allocate",
            AllocateAttributePoints
        );
        app.MapGet("/creatures/{creatureId:guid}/attributes", GetBaseAttributes);
        app.MapGet("/creatures/{creatureId:guid}/skills", GetSkills);
        app.MapGet("/creatures/{creatureId:guid}/level", GetLevel);
    }

    private static async Task<IResult> GetAbilities(
        Guid creatureId,
        GetUsableAbilitiesQueryHandler getUsableAbilities,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getUsableAbilities.Handle(
            new GetUsableAbilitiesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(abilities.Select(ToAbilitySummary).ToArray());
    }

    private static async Task<IResult> GetUsableItems(
        Guid creatureId,
        GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
        CancellationToken cancellationToken
    )
    {
        var inventoryItems = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = creatureId },
            cancellationToken
        );
        var items = UsableItem.FromInventory(inventoryItems);

        return Results.Ok(items.Select(ToUsableItemSummary).ToArray());
    }

    private static async Task<IResult> GetAttributePoints(
        Guid creatureId,
        GetUnallocatedAttributePointsQueryHandler getUnallocatedAttributePoints,
        CancellationToken cancellationToken
    )
    {
        var points = await getUnallocatedAttributePoints.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(new AttributePointsResponse(points));
    }

    private static async Task<IResult> AllocateAttributePoints(
        Guid creatureId,
        AllocateAttributePointsRequest request,
        AllocateAttributePointsCommandHandler allocateAttributePoints,
        CancellationToken cancellationToken
    )
    {
        var deltas = request.Deltas.ToDictionary(kv => kv.Key.ToDomain(), kv => kv.Value);

        await allocateAttributePoints.Handle(
            new AllocateAttributePointsCommand { CreatureId = creatureId, Deltas = deltas },
            cancellationToken
        );

        return Results.NoContent();
    }

    private static async Task<IResult> GetBaseAttributes(
        Guid creatureId,
        GetCreatureBaseAttributesQueryHandler getCreatureBaseAttributes,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureBaseAttributes.Handle(
            new GetCreatureBaseAttributesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(
            new BaseAttributesResponse(
                attributes.Strength,
                attributes.Defense,
                attributes.Dexterity,
                attributes.Endurance,
                attributes.Stamina,
                attributes.Mana,
                attributes.Intelligence
            )
        );
    }

    private static async Task<IResult> GetSkills(
        Guid creatureId,
        GetCreatureSkillsQueryHandler getCreatureSkills,
        CancellationToken cancellationToken
    )
    {
        var skills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(skills.Select(ToSkillProgressSummary).ToArray());
    }

    private static async Task<IResult> GetLevel(
        Guid creatureId,
        GetCreatureLevelQueryHandler getCreatureLevel,
        CancellationToken cancellationToken
    )
    {
        var level = await getCreatureLevel.Handle(
            new GetCreatureLevelQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(new CreatureLevelResponse(level));
    }

    private static UsableItemSummary ToUsableItemSummary(UsableItem item) =>
        new(item.Name, item.Resource.ToContract(), item.Amount);

    private static AbilitySummary ToAbilitySummary(Ability ability) =>
        new(
            ability.Name,
            ability.Skill.ToContract(),
            ability.Description,
            ability.ApCost,
            ability.MpCost,
            ability.Cooldown,
            ability is AttackAbility ? AbilityCategory.Offensive : AbilityCategory.Support
        );

    private static SkillProgressSummary ToSkillProgressSummary(CreatureSkillProgress progress) =>
        new(
            progress.Skill.ToContract(),
            progress.Level,
            progress.ExperienceCurrent,
            progress.ExperienceToNextLevel
        );
}
