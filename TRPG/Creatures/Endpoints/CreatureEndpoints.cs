using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Inventory.Responses;

namespace TRPG.Creatures.Endpoints;

internal static class CreatureEndpoints
{
    public static void MapCreatureEndpoints(this WebApplication app)
    {
        app.MapGet("/creatures/{creatureId:guid}/abilities", GetAbilities);
        app.MapGet("/creatures/{creatureId:guid}/items", GetUsableItems);
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
}
